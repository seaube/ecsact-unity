using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#nullable enable

namespace Ecsact {
	public enum AsyncError : Int32 {
		ConnectionClosed,
		ConnectFail,
		SocketFail,
		StateFail,
		StartFail,
		InvalidConnectionString,
	}
}

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
internal static class NativeLibrary {
	[DllImport("Kernel32.dll",
		EntryPoint = "LoadLibrary",
		CharSet = CharSet.Ansi,
		CallingConvention = CallingConvention.Winapi
	)]
	private static extern IntPtr LoadLibrary
		( [MarshalAs(UnmanagedType.LPStr)] string lpFileName
		);

	[DllImport("Kernel32.dll",
		EntryPoint = "FreeLibrary",
		CallingConvention = CallingConvention.Winapi
	)]
	private static extern bool FreeLibrary
		( IntPtr hLibModule
		);

	[DllImport("Kernel32.dll",
		EntryPoint = "GetProcAddress",
		CharSet = CharSet.Ansi,
		CallingConvention = CallingConvention.Winapi
	)]
	private static extern IntPtr GetProcAddress
		( IntPtr                                   hModule
		, [MarshalAs(UnmanagedType.LPStr)] string  procName
		);

	public static IntPtr Load
		( string libraryPath
		)
	{
		return LoadLibrary(libraryPath);
	}

	public static void Free
		( IntPtr handle
		)
	{
		FreeLibrary(handle);
	}

	public static bool TryGetExport
		( IntPtr      handle
		, string      name
		, out IntPtr  address
		)
	{
		address = GetProcAddress(handle, name);
		return address != IntPtr.Zero;
	}
}
#endif

public class EcsactRuntimeMissingMethod : Exception {
	public EcsactRuntimeMissingMethod
		( string methodName
		)
		: base(methodName)
	{
	}

	public EcsactRuntimeMissingMethod
		( string     methodName
		, Exception  inner
		)
		: base(methodName, inner)
	{
	}
}

public class EcsactRuntime {
	public static class VisualScriptingEventNames {
		public const string AsyncError = "EcsactAsyncErrorEvent";
		public const string AsyncConnectStateChange = "EcsactAsyncConnectStateChange";
	}

	public enum EcsactEvent : Int32 {
		InitComponent = 0,
		UpdateComponent = 1,
		RemoveComponent = 2,
	}

	public delegate void EachComponentCallback
		( Int32   componentId
		, object  componentData
		, IntPtr  callbackUserData
		);

	public delegate void ComponentEventCallback
		( EcsactEvent  ev
		, Int32        entityId
		, Int32        componentId
		, IntPtr       componentData
		, IntPtr       callbackUserData
		);

	public struct ExecutionOptions {
		public Int32 addComponentsLength;
		public Int32[] addComponentsEntities;
		public object[] addComponents;

		public Int32 updateComponentsLength;
		public Int32[] updateComponentsEntities;
		public object[] updateComponents;

		public Int32 removeComponentsLength;
		public Int32[] removeComponentsEntities;
		public Int32[] removeComponents;

		public Int32 actionsLength;
		public object[] actions;
	};

	public struct ExecutionEventsCollector {
		/// <summary>
		/// invoked after system executions are finished for every component that
		/// is new. The component_data is the last value given for the component, 
		/// not the first. Invocation happens in the calling thread. `event` will 
		/// always be <c>EcsactEvent.InitComponent</c>
		/// </summary>
		public ComponentEventCallback initCallback;

		/// <summary>
		/// <c>callbackUserData</c> passed to <c>initCallback</c>
		/// </summary>
		public IntPtr initCallbackUserData;

		/// <summary>
		/// invoked after system executions are finished for every changed 
		/// component. Invocation happens in the calling thread. <c>event</c> will 
		/// always be <c>EcsactEvent.UpdateComponent</c>
		/// </summary>
		public ComponentEventCallback updateCallback;

		/// <summary>
		/// <c>callbackUserData</c> passed to <c>updateCallback</c>
		/// </summary>
		public IntPtr updateCallbackUserData;

		/// <summary>
		/// invoked after system executions are finished for every removed 
		/// component. Invocation happens in the calling thread. <c>event</c> will
		/// always be <c>EcsactEvent.RemoveComponent</c>.
		/// </summary>
		public ComponentEventCallback removeCallback;

		/// <summary>
		/// <c>callbackUserData</c> passed to <c>removeCallback</c>
		/// </summary>
		public IntPtr removeCallbackUserData;
	}

	public struct StaticComponentInfo {
		public Int32 componentId;
		[MarshalAs(UnmanagedType.LPStr)] public string componentName;
		public Int32 componentSize;
		public ComponentCompareFn componentCompareFn;
		[MarshalAs(UnmanagedType.I1)] public bool transient;
	}

	public struct StaticSystemInfo {
		public Int32 systemId;
		public Int32 order;
		[MarshalAs(UnmanagedType.LPStr)] public string systemName;
		public Int32 parentSystemId;
		public Int32 childSystemsCount;
		public Int32[] childSystemIds;
		public Int32 capabilitiesCount;
		public Int32[] capabilityComponents;
		public SystemCapability[] capabilities;
		public SystemExecutionImpl executionImpl;
	}

	public struct StaticActionInfo {
		public Int32 actionId;
		public Int32 order;
		[MarshalAs(UnmanagedType.LPStr)] public string actionName;
		public Int32 actionSize;
		public ActionCompareFn actionCompareFn;
		public Int32 childSystemsCount;
		public Int32[] childSystemIds;
		public Int32 capabilitiesCount;
		public Int32[] capabilityComponents;
		public SystemCapability[] capabilities;
		public SystemExecutionImpl executionImpl;
	}

	public delegate void StaticReloadCallback
		( IntPtr userData
		);

	public enum SystemCapability : Int32 {
		Readonly = 1,
		Writeonly = 2,
		ReadWrite = 3,
		OptionalReadonly = 4 | Readonly,
		OptionalWriteonly = 4 | Writeonly,
		OptionalReadWrite = 4 | ReadWrite,
		Include = 8,
		Exclude = 16,
		Adds = 32 | Exclude,
		Removes = 64 | Include,
	}

	public enum SystemGenerate : Int32 {
		Required = 1,
		Optional = 2,
	}

	public delegate void SystemExecutionImpl
		( IntPtr context
		);

	public delegate Int32 ActionCompareFn
		( IntPtr  firstAction
		, IntPtr  secondAction
		);

	public delegate Int32 ComponentCompareFn
		( IntPtr  firstComponent
		, IntPtr  secondComponent
		);

	public delegate void AsyncErrorCallback
		( Ecsact.AsyncError  err
		, Int32              requestId
		, IntPtr             callbackUserData
		);

	public delegate void AsyncConnectCallback
		( [MarshalAs(UnmanagedType.LPStr)] string  connectAddress
		, Int32                                    connectPort
		, IntPtr                                   callbackUserData
		);

	public delegate void  AsyncActionCommittedCallback
		( Int32   actionId
		, object  actionData
		, Int32   committedTick
		, Int32   requestId
		, IntPtr  callbackUserData
		);

	public struct AsyncEventsCollector {
		public AsyncErrorCallback errorCallback;
		public IntPtr errorCallbackUserData;
		public AsyncConnectCallback connectCallback;
		public IntPtr connectCallbackUserData;
		public AsyncActionCommittedCallback actionCommittedCallback;
		public IntPtr actionCommittedCallbackUserData;
	}

	private static EcsactRuntime? defaultInstance;

	public static EcsactRuntime GetOrLoadDefault() {
		if(defaultInstance == null) {
			var settings = EcsactRuntimeSettings.Get();
			defaultInstance = Load(settings.runtimeLibraryPaths);

			if(defaultInstance != null) {
				foreach(var defReg in settings.defaultRegistries) {
					defReg.registryId = defaultInstance.core.CreateRegistry(
						defReg.registryName
					);
				}
			}
		}

		if(defaultInstance == null) {
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
			var okQuit = UnityEditor.EditorUtility.DisplayDialog(
				title: "Failed to load default ecsact runtime",
				message: "Please check your ecsact runtime settings",
				ok: "Ok Quit",
				cancel: "Continue Anyways"
			);

			if(okQuit) {
				UnityEditor.EditorApplication.isPlaying = false;
			}
			UnityEngine.Application.Quit(1);
#else
			UnityEngine.Debug.LogError("Failed to load default ecsact runtime");
			UnityEngine.Application.Quit(1);
#endif
			throw new Exception("Failed to load default ecsact runtime");
		}

		return defaultInstance;
	}

	public static void SetDefault
		( EcsactRuntime? runtime
		)
	{
		if(defaultInstance != null) {
			Free(defaultInstance);
		}
		defaultInstance = runtime;
	}

	private IntPtr[]? _libs;
	private Core? _core;
	private Async? _async;
	private Dynamic? _dynamic;
	private Meta? _meta;
	private Serialize? _serialize;
	private Static? _static;
	private Wasm? _wasm;

	public class Async {
		internal List<string> _availableMethods = new();
		public static string[] methods => new string[]{
			"ecsact_async_connect",
			"ecsact_async_disconnect",
			"ecsact_async_execute_action_at",
			"ecsact_async_execute_action",
			"ecsact_async_flush_events",
		};

		public IEnumerable<string> availableMethods => _availableMethods;

		internal delegate void ecsact_async_execute_action_delegate
			( Int32   actionId
			, IntPtr  actionData
			);
		internal ecsact_async_execute_action_delegate? ecsact_async_execute_action;

		internal delegate void ecsact_async_execute_action_at_delegate
			( Int32   actionId
			, IntPtr  actionData
			, Int32   tick
			);
		internal ecsact_async_execute_action_at_delegate? ecsact_async_execute_action_at;

		internal delegate void ecsact_async_flush_events_delegate
			( in ExecutionEventsCollector  executionEventsCollector
			, in AsyncEventsCollector      asyncEventsCollector
			);
		internal ecsact_async_flush_events_delegate? ecsact_async_flush_events;

		internal delegate void ecsact_async_connect_delegate
			( [MarshalAs(UnmanagedType.LPStr)] string connectionString
			);
		internal ecsact_async_connect_delegate? ecsact_async_connect;

		internal delegate void ecsact_async_disconnect_delegate
			(
			);
		internal ecsact_async_disconnect_delegate? ecsact_async_disconnect;

		public delegate void ErrorCallback
			( Ecsact.AsyncError  err
			, Int32              requestId
			);

		public delegate void ConnectCallback
			( string  connectAddress
			, Int32   connectPort
			);

		private AsyncEventsCollector _asyncEvs;
		private List<ErrorCallback> _errCallbacks = new();
		private List<ConnectCallback> _connectCallbacks = new();
		private EcsactRuntime _owner;

		internal Async
			( EcsactRuntime owner
			)
		{
			_owner = owner;
			_asyncEvs = new AsyncEventsCollector{
				actionCommittedCallback = OnActionCommittedHandler,
				actionCommittedCallbackUserData = IntPtr.Zero,
				errorCallback = OnErrorHandler,
				errorCallbackUserData = IntPtr.Zero,
				connectCallback = OnConnectHandler,
				connectCallbackUserData = IntPtr.Zero,
			};
		}

		[AOT.MonoPInvokeCallback(typeof(AsyncActionCommittedCallback))]
		private static void OnActionCommittedHandler
			( Int32   actionId
			, object  actionData
			, Int32   committedTick
			, Int32   requestId
			, IntPtr  callbackUserData
			)
		{
			var self = (GCHandle.FromIntPtr(callbackUserData).Target as Async)!;
		}

		[AOT.MonoPInvokeCallback(typeof(AsyncErrorCallback))]
		private static void OnErrorHandler
			( Ecsact.AsyncError  err
			, Int32              requestId
			, IntPtr             callbackUserData
			)
		{
			var self = (GCHandle.FromIntPtr(callbackUserData).Target as Async)!;
			foreach(var cb in self._errCallbacks) {
				cb(err, requestId);
			}
		}

		[AOT.MonoPInvokeCallback(typeof(AsyncConnectCallback))]
		private static void OnConnectHandler
			( [MarshalAs(UnmanagedType.LPStr)] string  connectAddress
			, Int32                                    connectPort
			, IntPtr                                   callbackUserData
			)
		{
			var self = (GCHandle.FromIntPtr(callbackUserData).Target as Async)!;
			foreach(var cb in self._connectCallbacks) {
				cb(connectAddress, connectPort);
			}
		}

		public Action OnError
			( ErrorCallback callback
			)
		{
			_errCallbacks.Add(callback);

			return () => {
				_errCallbacks.Remove(callback);
			};
		}

		public Action OnConnect
			( ConnectCallback callback
			)
		{
			_connectCallbacks.Add(callback);

			return () => {
				_connectCallbacks.Remove(callback);
			};
		}

		public void Connect
			( string connectionString
			)
		{
			if(ecsact_async_connect == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_async_connect");
			}
			ecsact_async_connect(connectionString);
		}

		public void FlushEvents() {
			if(ecsact_async_flush_events == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_async_flush_events");
			}

			var selfPinned = GCHandle.Alloc(this, GCHandleType.Pinned);
			var ownerPinned = GCHandle.Alloc(_owner, GCHandleType.Pinned);
			try {
				var selfIntPtr = GCHandle.ToIntPtr(selfPinned);
				var ownerIntPtr = GCHandle.ToIntPtr(ownerPinned);
				_owner._execEvs.initCallbackUserData = ownerIntPtr;
				_owner._execEvs.updateCallbackUserData = ownerIntPtr;
				_owner._execEvs.removeCallbackUserData = ownerIntPtr;
				_asyncEvs.errorCallbackUserData = selfIntPtr;
				_asyncEvs.connectCallbackUserData = selfIntPtr;
				_asyncEvs.actionCommittedCallbackUserData = selfIntPtr;
				ecsact_async_flush_events(in _owner._execEvs, in _asyncEvs);
			} finally {
				selfPinned.Free();
				ownerPinned.Free();
			}
		}
	}

	public class Core {
		internal List<string> _availableMethods = new();
		public static string[] methods => new string[]{
			"ecsact_add_component",
			"ecsact_clear_registry",
			"ecsact_count_components",
			"ecsact_count_entities",
			"ecsact_create_entity",
			"ecsact_create_registry",
			"ecsact_destroy_entity",
			"ecsact_destroy_registry",
			"ecsact_each_component",
			"ecsact_ensure_entity",
			"ecsact_entity_exists",
			"ecsact_execute_systems",
			"ecsact_get_component",
			"ecsact_get_components",
			"ecsact_get_entities",
			"ecsact_has_component",
			"ecsact_remove_component",
			"ecsact_update_component",
		};

		public IEnumerable<string> availableMethods => _availableMethods;

		internal delegate Int32 ecsact_create_registry_delegate
			( string registryName
			);
		internal ecsact_create_registry_delegate? ecsact_create_registry;

		internal delegate void ecsact_destroy_registry_delegate
			( Int32 registryId
			);
		internal ecsact_destroy_registry_delegate? ecsact_destroy_registry;

		internal delegate void ecsact_clear_registry_delegate
			( Int32 registryId
			);
		internal ecsact_clear_registry_delegate? ecsact_clear_registry;

		internal delegate Int32 ecsact_create_entity_delegate
			( Int32 registryId
			);
		internal ecsact_create_entity_delegate? ecsact_create_entity;

		internal delegate void ecsact_ensure_entity_delegate
			( Int32 registryId
			, Int32 entityId
			);
		internal ecsact_ensure_entity_delegate? ecsact_ensure_entity;

		internal delegate bool ecsact_entity_exists_delegate
			( Int32 registryId
			, Int32 entityId
			);
		internal ecsact_entity_exists_delegate? ecsact_entity_exists;

		internal delegate void ecsact_destroy_entity_delegate
			( Int32 registryId
			, Int32 entityId
			);
		internal ecsact_destroy_entity_delegate? ecsact_destroy_entity;

		internal delegate Int32 ecsact_count_entities_delegate
			( Int32 registryId
			);
		internal ecsact_count_entities_delegate? ecsact_count_entities;

		internal delegate void ecsact_get_entities_delegate
			( Int32        registryId
			, Int32        maxEntitiesCount
			, out Int32[]  outEntities
			, out Int32    outEntitiesCount
			);
		internal ecsact_get_entities_delegate? ecsact_get_entities;

		internal delegate void ecsact_add_component_delegate
			( Int32   registryId
			, Int32   entityId
			, Int32   componentId
			, IntPtr  componentData
			);
		internal ecsact_add_component_delegate? ecsact_add_component;

		internal delegate bool ecsact_has_component_delegate
			( Int32  registryId
			, Int32  entityId
			, Int32  componentId
			);
		internal ecsact_has_component_delegate? ecsact_has_component;

		internal delegate IntPtr ecsact_get_component_delegate
			( Int32  registryId
			, Int32  entityId
			, Int32  componentId
			);
		internal ecsact_get_component_delegate? ecsact_get_component;

		internal delegate void ecsact_each_component_delegate
			( Int32                  registryId
			, Int32                  entityId
			, EachComponentCallback  callback
			, IntPtr                 callbackUserData
			);
		internal ecsact_each_component_delegate? ecsact_each_component;

		internal delegate Int32 ecsact_count_components_delegate
			( Int32  registryId
			, Int32  entityId
			);
		internal ecsact_count_components_delegate? ecsact_count_components;

		internal delegate void ecsact_get_components_delegate
			( Int32         registryId
			, Int32         entityId
			, Int32         maxComponentsCount
			, out Int32[]   outComponentIds
			, out IntPtr[]  outComponentsData
			, out Int32     outComponentsCount
			);
		internal ecsact_get_components_delegate? ecsact_get_components;

		internal delegate void ecsact_update_component_delegate
			( Int32   registryId
			, Int32   entityId
			, Int32   componentId
			, object  componentData
			);
		internal ecsact_update_component_delegate? ecsact_update_component;

		internal delegate void ecsact_remove_component_delegate
			( Int32  registryId
			, Int32  entityId
			, Int32  componentId
			);
		internal ecsact_remove_component_delegate? ecsact_remove_component;

		internal delegate void ecsact_component_event_callback
			( EcsactEvent  ev
			, Int32        entityId
			, Int32        componentId
			, object       componentData
			, IntPtr       callbackUserData
			);
		internal delegate void ecsact_execute_systems_delegate
			( Int32                        registryId
			, Int32                        executionCount
			, ExecutionOptions[]           executionOptionsList
			, in ExecutionEventsCollector  eventsCollector
			);
		internal ecsact_execute_systems_delegate? ecsact_execute_systems;

		private EcsactRuntime _owner;

		internal Core
			( EcsactRuntime owner
			)
		{
			_owner = owner;
		}

		public Int32 CreateRegistry
			( string registryName
			)
		{
			if(ecsact_create_registry == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_create_registry");
			}

			return ecsact_create_registry(registryName);
		}

		public void DestroyRegistry
			( Int32 registryId
			)
		{
			if(ecsact_destroy_registry == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_destroy_registry");
			}

			ecsact_destroy_registry(registryId);
		}

		public void ClearRegistry
			( Int32 registryId
			)
		{
			if(ecsact_clear_registry == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_clear_registry");
			}

			ecsact_clear_registry(registryId);
		}

		public Int32 CreateEntity
			( Int32 registryId
			)
		{
			if(ecsact_create_entity == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_create_entity");
			}

			return ecsact_create_entity(registryId);
		}

		public void EnsureEntity
			( Int32 registryId
			, Int32 entityId
			)
		{
			if(ecsact_ensure_entity == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_ensure_entity");
			}

			ecsact_ensure_entity(registryId, entityId);
		}

		public bool EntityExists
			( Int32 registryId
			, Int32 entityId
			)
		{
			if(ecsact_entity_exists == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_entity_exists");
			}

			return ecsact_entity_exists(registryId, entityId);
		}

		public void DestroyEntity
			( Int32 registryId
			, Int32 entityId
			)
		{
			if(ecsact_destroy_entity == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_destroy_entity");
			}

			ecsact_destroy_entity(registryId, entityId);
		}

		public Int32 CountEntities
			( Int32 registryId
			)
		{
			if(ecsact_count_entities == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_count_entities");
			}

			return ecsact_count_entities(registryId);
		}

		public void GetEntities
			( Int32        registryId
			, Int32        maxEntitiesCount
			, out Int32[]  outEntities
			, out Int32    outEntitiesCount
			)
		{
			if(ecsact_get_entities == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_get_entities");
			}

			ecsact_get_entities(
				registryId,
				maxEntitiesCount,
				out outEntities,
				out outEntitiesCount
			);
		}

		public Int32[] GetEntities
			( Int32 registryId
			)
		{
			var entitiesCount = CountEntities(registryId);
			var entities = new Int32[entitiesCount];

			GetEntities(
				registryId,
				entitiesCount,
				out entities,
				out entitiesCount
			);

			return entities;
		}

		public void AddComponent<C>
			( Int32  registryId
			, Int32  entityId
			, C      component
			) where C : Ecsact.Component
		{
			if(ecsact_add_component == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_add_component");
			}

			var componentId = Ecsact.Util.GetComponentID<C>();
			var componentPtr = Marshal.AllocHGlobal(Marshal.SizeOf(component));

			try {
				Marshal.StructureToPtr(component, componentPtr, false);
				ecsact_add_component(
					registryId,
					entityId,
					componentId,
					componentPtr
				);
			} finally {
				Marshal.FreeHGlobal(componentPtr);
			}
		}

		public bool HasComponent
			( Int32   registryId
			, Int32   entityId
			, Int32   componentId
			)
		{
			if(ecsact_has_component == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_has_component");
			}

			return ecsact_has_component(registryId, entityId, componentId);
		}

		public IntPtr GetComponent
			( Int32   registryId
			, Int32   entityId
			, Int32   componentId
			)
		{
			if(ecsact_get_component == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_get_component");
			}

			return ecsact_get_component(registryId, entityId, componentId);
		}

		public Int32 CountComponents
			( Int32  registryId
			, Int32  entityId
			)
		{
			if(ecsact_count_components == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_count_components");
			}

			return ecsact_count_components(registryId, entityId);
		}

		public void GetComponents
			( Int32         registryId
			, Int32         entityId
			, Int32         maxComponentsCount
			, out Int32[]   outComponentIds
			, out IntPtr[]  outComponentsData
			, out Int32     outComponentsCount
			)
		{
			if(ecsact_get_components == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_get_components");
			}

			ecsact_get_components(
				registryId,
				entityId,
				maxComponentsCount,
				out outComponentIds,
				out outComponentsData,
				out outComponentsCount
			);
		}

		public void EachComponent
			( Int32                  registryId
			, Int32                  entityId
			, EachComponentCallback  callback
			, IntPtr                 callbackUserData
			)
		{
			if(ecsact_each_component == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_each_component");
			}

			ecsact_each_component(
				registryId,
				entityId,
				callback,
				callbackUserData
			);
		}

		public void UpdateComponent
			( Int32   registryId
			, Int32   entityId
			, Int32   componentId
			, object  componentData
			)
		{
			if(ecsact_update_component == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_update_component");
			}

			ecsact_update_component(
				registryId,
				entityId,
				componentId,
				componentData
			);
		}

		public void RemoveComponent
			( Int32  registryId
			, Int32  entityId
			, Int32  componentId
			)
		{
			if(ecsact_remove_component == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_remove_component");
			}

			ecsact_remove_component(registryId, entityId, componentId);
		}

		public void ExecuteSystems
			( Int32               registryId
			, Int32               executionCount
			, ExecutionOptions[]  executionOptionsList
			)
		{
			if(ecsact_execute_systems == null) {
				throw new EcsactRuntimeMissingMethod("ecsact_execute_systems");
			}

			var ownerPinned = GCHandle.Alloc(_owner, GCHandleType.Pinned);

			try {
				var ownerIntPtr = GCHandle.ToIntPtr(ownerPinned);
				_owner._execEvs.initCallbackUserData = ownerIntPtr;
				_owner._execEvs.updateCallbackUserData = ownerIntPtr;
				_owner._execEvs.removeCallbackUserData = ownerIntPtr;
				ecsact_execute_systems(
					registryId,
					executionCount,
					executionOptionsList,
					in _owner._execEvs
				);
			} finally {
				ownerPinned.Free();
			}
		}
	}

	public class Dynamic {
		internal List<string> _availableMethods = new();
		public static string[] methods => new string[]{
			"ecsact_add_system_capability",
			"ecsact_add_system_generate_component_set",
			"ecsact_create_action",
			"ecsact_create_component",
			"ecsact_create_system",
			"ecsact_create_variant",
			"ecsact_destroy_component",
			"ecsact_destroy_variant",
			"ecsact_register_action",
			"ecsact_register_component",
			"ecsact_register_system",
			"ecsact_remove_system_capability",
			"ecsact_resize_action",
			"ecsact_resize_component",
			"ecsact_set_system_execution_impl",
			"ecsact_system_execution_context_action",
			"ecsact_system_execution_context_add",
			"ecsact_system_execution_context_generate",
			"ecsact_system_execution_context_get",
			"ecsact_system_execution_context_has",
			"ecsact_system_execution_context_id",
			"ecsact_system_execution_context_parent",
			"ecsact_system_execution_context_remove",
			"ecsact_system_execution_context_same",
			"ecsact_update_system_capability",
		};

		public IEnumerable<string> availableMethods => _availableMethods;

		internal delegate void ecsact_system_execution_context_action_delegate
			( IntPtr  context
			, IntPtr  outActionData
			);
		internal ecsact_system_execution_context_action_delegate? ecsact_system_execution_context_action;

		internal delegate void ecsact_system_execution_context_add_delegate
			( IntPtr  context
			, Int32   componentId
			, object  componentData
			);
		internal ecsact_system_execution_context_add_delegate? ecsact_system_execution_context_add;

		internal delegate void ecsact_system_execution_context_remove_delegate
			( IntPtr  context
			, Int32   componentId
			);
		internal ecsact_system_execution_context_remove_delegate? ecsact_system_execution_context_remove;

		internal delegate void ecsact_system_execution_context_get_delegate
			( IntPtr      context
			, Int32       componentId
			, out object  outComponentData
			);
		internal ecsact_system_execution_context_get_delegate? ecsact_system_execution_context_get;

		internal delegate void ecsact_system_execution_context_update_delegate
			( IntPtr  context
			, Int32   componentId
			, object  componentData
			);
		internal ecsact_system_execution_context_update_delegate? ecsact_system_execution_context_update;

		internal delegate bool ecsact_system_execution_context_has_delegate
			( IntPtr  context
			, Int32   componentId
			);
		internal ecsact_system_execution_context_has_delegate? ecsact_system_execution_context_has;

		internal delegate void ecsact_system_execution_context_generate_delegate
			( IntPtr    context
			, Int32     componentCount
			, Int32[]   componentIds
			, object[]  componentsData
			);
		internal ecsact_system_execution_context_generate_delegate? ecsact_system_execution_context_generate;

		internal delegate IntPtr ecsact_system_execution_context_parent_delegate
			( IntPtr context
			);
		internal ecsact_system_execution_context_parent_delegate? ecsact_system_execution_context_parent;

		internal delegate bool ecsact_system_execution_context_same_delegate
			( IntPtr  firstContext
			, IntPtr  secondContext
			);
		internal ecsact_system_execution_context_same_delegate? ecsact_system_execution_context_same;

		internal delegate void ecsact_create_system_delegate
			( [MarshalAs(UnmanagedType.LPStr)] string  systemName
			, Int32                                    parentSystemId
			, Int32[]                                  capabilityComponentIds
			, SystemCapability[]                       capabilities
			, Int32                                    capabilitiesCount
			, SystemExecutionImpl                      executionImpl
			);
		internal ecsact_create_system_delegate? ecsact_create_system;

		internal delegate void ecsact_set_system_execution_impl_delegate
			( Int32                systemId
			, SystemExecutionImpl  executionImpl
			);
		internal ecsact_set_system_execution_impl_delegate? ecsact_set_system_execution_impl;

		internal delegate void ecsact_create_action_delegate
			( [MarshalAs(UnmanagedType.LPStr)] string  actionName
			, Int32                                    actionSize
			, ActionCompareFn                          actionCompareFn
			, Int32[]                                  capabilityComponentIds
			, SystemCapability[]                       capabilities
			, Int32                                    capabilitiesCount
			, SystemExecutionImpl                      executionImpl
			);
		internal ecsact_create_action_delegate? ecsact_create_action;

		internal delegate void ecsact_resize_action_delegate
			( Int32            actionId
			, Int32            newActionSize
			, ActionCompareFn  newActionCompareFn
			);
		internal ecsact_resize_action_delegate? ecsact_resize_action;

		internal delegate void ecsact_create_component_delegate
			( [MarshalAs(UnmanagedType.LPStr)] string  componentName
			, Int32                                    componentSize
			, ComponentCompareFn                       componentCompareFn
			);
		internal ecsact_create_component_delegate? ecsact_create_component;

		internal delegate void ecsact_resize_component_delegate
			( Int32               componentId
			, Int32               newComponentSize
			, ComponentCompareFn  newComponentCompareFn
			);
		internal ecsact_resize_component_delegate? ecsact_resize_component;

		internal delegate void ecsact_destroy_component_delegate
			( Int32 componentId
			);
		internal ecsact_destroy_component_delegate? ecsact_destroy_component;

		internal delegate void ecsact_add_system_capability_delegate
			( Int32             systemId
			, Int32             componentId
			, SystemCapability  systemCapability
			);
		internal ecsact_add_system_capability_delegate? ecsact_add_system_capability;

		internal delegate void ecsact_update_system_capability_delegate
			( Int32             systemId
			, Int32             componentId
			, SystemCapability  systemCapability
			);
		internal ecsact_update_system_capability_delegate? ecsact_update_system_capability;

		internal delegate void ecsact_remove_system_capability_delegate
			( Int32  systemId
			, Int32  componentId
			);
		internal ecsact_remove_system_capability_delegate? ecsact_remove_system_capability;

		internal delegate void ecsact_add_system_generate_component_set_delegate
			( Int32             systemId
			, Int32             componentsCount
			, Int32[]           componentIds
			, SystemGenerate[]  componentGenerateFlags
			);
		internal ecsact_add_system_generate_component_set_delegate? ecsact_add_system_generate_component_set;

		internal delegate void ecsact_register_component_delegate
			( Int32  registryId
			, Int32  componentId
			);
		internal ecsact_register_component_delegate? ecsact_register_component;

		internal delegate void ecsact_register_system_delegate
			( Int32  registryId
			, Int32  systemId
			);
		internal ecsact_register_system_delegate? ecsact_register_system;

		internal delegate void ecsact_register_action_delegate
			( Int32  registryId
			, Int32  actionId
			);
		internal ecsact_register_action_delegate? ecsact_register_action;

		internal delegate Int32 ecsact_system_execution_context_id_delegate
			( IntPtr context
			);
		internal ecsact_system_execution_context_id_delegate? ecsact_system_execution_context_id;
	}

	public class Meta {
		internal List<string> _availableMethods = new();
		public static string[] methods => new string[]{
			"ecsact_meta_action_name",
			"ecsact_meta_action_size",
			"ecsact_meta_component_name",
			"ecsact_meta_component_size",
			"ecsact_meta_registry_name",
			"ecsact_meta_system_capabilities_count",
			"ecsact_meta_system_capabilities",
			"ecsact_meta_system_name",
		};

		public IEnumerable<string> availableMethods => _availableMethods;

		[return: MarshalAs(UnmanagedType.LPStr)]
		internal delegate string ecsact_meta_registry_name_delegate
			( Int32 registryId
			);
		internal ecsact_meta_registry_name_delegate? ecsact_meta_registry_name;

		internal delegate Int32 ecsact_meta_component_size_delegate
			( Int32 componentId
			);
		internal ecsact_meta_component_size_delegate? ecsact_meta_component_size;

		[return: MarshalAs(UnmanagedType.LPStr)]
		internal delegate string ecsact_meta_component_name_delegate
			( Int32 componentId
			);
		internal ecsact_meta_component_name_delegate? ecsact_meta_component_name;

		internal delegate Int32 ecsact_meta_action_size_delegate
			( Int32 actionId
			);
		internal ecsact_meta_action_size_delegate? ecsact_meta_action_size;

		[return: MarshalAs(UnmanagedType.LPStr)]
		internal delegate string ecsact_meta_action_name_delegate
			( Int32 actionId
			);
		internal ecsact_meta_action_name_delegate? ecsact_meta_action_name;

		[return: MarshalAs(UnmanagedType.LPStr)]
		internal delegate string ecsact_meta_system_name_delegate
			( Int32 systemId
			);
		internal ecsact_meta_system_name_delegate? ecsact_meta_system_name;

		internal delegate Int32 ecsact_meta_system_capabilities_count_delegate
			( Int32 systemId
			);
		internal ecsact_meta_system_capabilities_count_delegate? ecsact_meta_system_capabilities_count;

		internal delegate void ecsact_meta_system_capabilities_delegate
			( Int32                   systemId
			, out Int32[]             outCapabilityComponentIds
			, out SystemCapability[]  outCapabilities
			);
		internal ecsact_meta_system_capabilities_delegate? ecsact_meta_system_capabilities;
	}

	public class Serialize {
		internal List<string> _availableMethods = new();
		public static string[] methods => new string[]{
			"ecsact_deserialize_action",
			"ecsact_deserialize_component",
			"ecsact_serialize_action_size",
			"ecsact_serialize_action",
			"ecsact_serialize_component_size",
			"ecsact_serialize_component",
		};

		public IEnumerable<string> availableMethods => _availableMethods;

		internal delegate Int32 ecsact_serialize_action_size_delegate
			( Int32 actionId
			);
		internal ecsact_serialize_action_size_delegate? ecsact_serialize_action_size;

		internal delegate Int32 ecsact_serialize_component_size_delegate
			( Int32 componentId
			);
		internal ecsact_serialize_component_size_delegate? ecsact_serialize_component_size;

		internal delegate void ecsact_serialize_action_delegate
			( Int32   actionId
			, IntPtr  actionData
			, IntPtr  outBytes
			);
		internal ecsact_serialize_action_delegate? ecsact_serialize_action;

		internal delegate void ecsact_serialize_component_delegate
			( Int32   componentId
			, object  inComponentData
			, IntPtr  outBytes
			);
		internal ecsact_serialize_component_delegate? ecsact_serialize_component;

		internal delegate void ecsact_deserialize_action_delegate
			( Int32   actionId
			, IntPtr  inBytes
			, IntPtr  outActionData
			);
		internal ecsact_deserialize_action_delegate? ecsact_deserialize_action;

		internal delegate void ecsact_deserialize_component_delegate
			( Int32       componentId
			, IntPtr      inBytes
			, out object  outComponentData
			);
		internal ecsact_deserialize_component_delegate? ecsact_deserialize_component;
	}

	public class Static {
		internal List<string> _availableMethods = new();
		public static string[] methods => new string[]{
			"ecsact_static_actions",
			"ecsact_static_components",
			"ecsact_static_off_reload",
			"ecsact_static_on_reload",
			"ecsact_static_systems",
		};

		public IEnumerable<string> availableMethods => _availableMethods;

		internal delegate void ecsact_static_components_delegate
			( out StaticComponentInfo[]  outComponents
			, out Int32                  outComponentsCount
			);
		internal ecsact_static_components_delegate? ecsact_static_components;

		internal delegate void ecsact_static_systems_delegate
			( out StaticSystemInfo[]  outSystems
			, out Int32               outSystemsCount
			);
		internal ecsact_static_systems_delegate? ecsact_static_systems;

		internal delegate void ecsact_static_actions_delegate
			( out StaticActionInfo[]  outActions
			, out Int32               outActionsCount
			);
		internal ecsact_static_actions_delegate? ecsact_static_actions;

		internal delegate void ecsact_static_on_reload_delegate
			( StaticReloadCallback  callback
			, IntPtr                callbackUserData
			);
		internal ecsact_static_on_reload_delegate? ecsact_static_on_reload;

		internal delegate void ecsact_static_off_reload_delegate
			( StaticReloadCallback callback
			);
		internal ecsact_static_off_reload_delegate? ecsact_static_off_reload;
	}

	// TODO(zaucy): This is misplaced here. It should be organized somewhere else
	//              maybe in another package
	// ecsactsi_* fns
	public class Wasm {
		public enum Error {
			Ok,
			OpenFail,
			ReadFail,
			CompileFail,
			InstantiateFail,
			ExportNotFound,
			ExportInvalid,
			GuestImportUnknown,
		}

		internal List<string> _availableMethods = new();
		public static string[] methods => new string[]{
			"ecsactsi_wasm_load",
			"ecsactsi_wasm_load_file",
			"ecsactsi_wasm_set_trap_handler",
		};

		public IEnumerable<string> availableMethods => _availableMethods;

		internal delegate void ecsactsi_wasm_load_delegate
			( sbyte[]   wasmData
			, Int32     wasmDataSize
			, Int32     systemsCount
			, Int32[]   systmIds
			, string[]  wasmExports
			);

		internal ecsactsi_wasm_load_delegate? ecsactsi_wasm_load;

		internal delegate void ecsactsi_wasm_load_file_delegate
			( [MarshalAs(UnmanagedType.LPStr)] string    wasmFilePath
			, Int32                                      systemsCount
			, Int32[]                                    systmIds
			, string[]                                   wasmExports
			);

		internal ecsactsi_wasm_load_file_delegate? ecsactsi_wasm_load_file;

		internal delegate void ecsactsi_wasm_trap_handler
			( Int32                                    systemId
			, [MarshalAs(UnmanagedType.LPStr)] string  trapMessage
			);

		internal delegate void ecsactsi_wasm_set_trap_handler_delegate
			( ecsactsi_wasm_trap_handler handler
			);

		internal ecsactsi_wasm_set_trap_handler_delegate? ecsactsi_wasm_set_trap_handler;

		public void Load
			( byte[]  wasmData
			, Int32   systemId
			, string  exportName
			)
		{
			if(ecsactsi_wasm_load == null) {
				throw new EcsactRuntimeMissingMethod("ecsactsi_wasm_load");
			}

			var systemIds = new Int32[]{systemId};
			var exportNames = new string[]{exportName};

			ecsactsi_wasm_load(
				(sbyte[]) (Array) wasmData,
				wasmData.Length,
				1,
				systemIds,
				exportNames
			);
		}
	}

	public Core core => _core!;
	public Async async => _async!;
	public Dynamic dynamic => _dynamic!;
	public Meta meta => _meta!;
	public Serialize serialize => _serialize!;
	public Static @static => _static!;
	public Wasm wasm => _wasm!;

	[AOT.MonoPInvokeCallback(typeof(Wasm.ecsactsi_wasm_trap_handler))]
	private static void DefaultWasmTrapHandler
		( Int32                                    systemId
		, [MarshalAs(UnmanagedType.LPStr)] string  trapMessage
		)
	{
		UnityEngine.Debug.LogError(
			$"[Wasm Trap (systemId={systemId})] {trapMessage}"
		);
	}

	private static void LoadDelegate<D>
		( IntPtr        lib
		, string        name
		, out D?        outDelegate
		, List<string>  availableMethods
		) where D : Delegate 
	{
		IntPtr addr;
		if(NativeLibrary.TryGetExport(lib, name, out addr)) {
			outDelegate = Marshal.GetDelegateForFunctionPointer<D>(addr);
			if(outDelegate != null) {
				availableMethods.Add(name);
			} else {
				UnityEngine.Debug.LogError(
					$"{name} is not a function in runtime library"
				);
			}
		} else {
			outDelegate = null;
		}
	}

	public static EcsactRuntime Load
		( IEnumerable<string> libraryPaths
		)
	{
		var runtime = new EcsactRuntime();
		runtime._core = new Core(runtime);
		runtime._async = new Async(runtime);
		runtime._dynamic = new Dynamic();
		runtime._meta = new Meta();
		runtime._serialize = new Serialize();
		runtime._static = new Static();
		runtime._wasm = new Wasm();
		runtime._libs =
			libraryPaths.Select(path => NativeLibrary.Load(path)).ToArray();

		foreach(var lib in runtime._libs) {
			// Load async methods
			LoadDelegate(lib, "ecsact_async_execute_action", out runtime._async.ecsact_async_execute_action, runtime._async._availableMethods);
			LoadDelegate(lib, "ecsact_async_execute_action_at", out runtime._async.ecsact_async_execute_action_at, runtime._async._availableMethods);
			LoadDelegate(lib, "ecsact_async_flush_events", out runtime._async.ecsact_async_flush_events, runtime._async._availableMethods);
			LoadDelegate(lib, "ecsact_async_connect", out runtime._async.ecsact_async_connect, runtime._async._availableMethods);
			LoadDelegate(lib, "ecsact_async_disconnect", out runtime._async.ecsact_async_disconnect, runtime._async._availableMethods);

			// Load core methods
			LoadDelegate(lib, "ecsact_create_registry", out runtime._core.ecsact_create_registry, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_destroy_registry", out runtime._core.ecsact_destroy_registry, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_clear_registry", out runtime._core.ecsact_clear_registry, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_create_entity", out runtime._core.ecsact_create_entity, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_ensure_entity", out runtime._core.ecsact_ensure_entity, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_entity_exists", out runtime._core.ecsact_entity_exists, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_destroy_entity", out runtime._core.ecsact_destroy_entity, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_count_entities", out runtime._core.ecsact_count_entities, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_get_entities", out runtime._core.ecsact_get_entities, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_add_component", out runtime._core.ecsact_add_component, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_has_component", out runtime._core.ecsact_has_component, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_get_component", out runtime._core.ecsact_get_component, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_each_component", out runtime._core.ecsact_each_component, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_count_components", out runtime._core.ecsact_count_components, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_get_components", out runtime._core.ecsact_get_components, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_update_component", out runtime._core.ecsact_update_component, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_remove_component", out runtime._core.ecsact_remove_component, runtime._core._availableMethods);
			LoadDelegate(lib, "ecsact_execute_systems", out runtime._core.ecsact_execute_systems, runtime._core._availableMethods);

			// Load dynamic methods
			LoadDelegate(lib, "ecsact_system_execution_context_action", out runtime._dynamic.ecsact_system_execution_context_action, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_system_execution_context_add", out runtime._dynamic.ecsact_system_execution_context_add, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_system_execution_context_remove", out runtime._dynamic.ecsact_system_execution_context_remove, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_system_execution_context_update", out runtime._dynamic.ecsact_system_execution_context_update, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_system_execution_context_get", out runtime._dynamic.ecsact_system_execution_context_get, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_system_execution_context_has", out runtime._dynamic.ecsact_system_execution_context_has, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_system_execution_context_generate", out runtime._dynamic.ecsact_system_execution_context_generate, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_system_execution_context_parent", out runtime._dynamic.ecsact_system_execution_context_parent, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_system_execution_context_same", out runtime._dynamic.ecsact_system_execution_context_same, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_create_system", out runtime._dynamic.ecsact_create_system, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_set_system_execution_impl", out runtime._dynamic.ecsact_set_system_execution_impl, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_create_action", out runtime._dynamic.ecsact_create_action, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_resize_action", out runtime._dynamic.ecsact_resize_action, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_create_component", out runtime._dynamic.ecsact_create_component, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_resize_component", out runtime._dynamic.ecsact_resize_component, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_destroy_component", out runtime._dynamic.ecsact_destroy_component, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_add_system_capability", out runtime._dynamic.ecsact_add_system_capability, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_update_system_capability", out runtime._dynamic.ecsact_update_system_capability, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_remove_system_capability", out runtime._dynamic.ecsact_remove_system_capability, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_add_system_generate_component_set", out runtime._dynamic.ecsact_add_system_generate_component_set, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_register_component", out runtime._dynamic.ecsact_register_component, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_register_system", out runtime._dynamic.ecsact_register_system, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_register_action", out runtime._dynamic.ecsact_register_action, runtime._dynamic._availableMethods);
			LoadDelegate(lib, "ecsact_system_execution_context_id", out runtime._dynamic.ecsact_system_execution_context_id, runtime._dynamic._availableMethods);

			// Load meta methods
			LoadDelegate(lib, "ecsact_meta_registry_name", out runtime._meta.ecsact_meta_registry_name, runtime._meta._availableMethods);
			LoadDelegate(lib, "ecsact_meta_component_size", out runtime._meta.ecsact_meta_component_size, runtime._meta._availableMethods);
			LoadDelegate(lib, "ecsact_meta_component_name", out runtime._meta.ecsact_meta_component_name, runtime._meta._availableMethods);
			LoadDelegate(lib, "ecsact_meta_action_size", out runtime._meta.ecsact_meta_action_size, runtime._meta._availableMethods);
			LoadDelegate(lib, "ecsact_meta_action_name", out runtime._meta.ecsact_meta_action_name, runtime._meta._availableMethods);
			LoadDelegate(lib, "ecsact_meta_system_name", out runtime._meta.ecsact_meta_system_name, runtime._meta._availableMethods);
			LoadDelegate(lib, "ecsact_meta_system_capabilities_count", out runtime._meta.ecsact_meta_system_capabilities_count, runtime._meta._availableMethods);
			LoadDelegate(lib, "ecsact_meta_system_capabilities", out runtime._meta.ecsact_meta_system_capabilities, runtime._meta._availableMethods);

			// Load serialize methods
			LoadDelegate(lib, "ecsact_serialize_action_size", out runtime._serialize.ecsact_serialize_action_size, runtime._serialize._availableMethods);
			LoadDelegate(lib, "ecsact_serialize_component_size", out runtime._serialize.ecsact_serialize_component_size, runtime._serialize._availableMethods);
			LoadDelegate(lib, "ecsact_serialize_action", out runtime._serialize.ecsact_serialize_action, runtime._serialize._availableMethods);
			LoadDelegate(lib, "ecsact_serialize_component", out runtime._serialize.ecsact_serialize_component, runtime._serialize._availableMethods);
			LoadDelegate(lib, "ecsact_deserialize_action", out runtime._serialize.ecsact_deserialize_action, runtime._serialize._availableMethods);
			LoadDelegate(lib, "ecsact_deserialize_component", out runtime._serialize.ecsact_deserialize_component, runtime._serialize._availableMethods);

			// Load static methods
			LoadDelegate(lib, "ecsact_static_components", out runtime._static.ecsact_static_components, runtime._static._availableMethods);
			LoadDelegate(lib, "ecsact_static_systems", out runtime._static.ecsact_static_systems, runtime._static._availableMethods);
			LoadDelegate(lib, "ecsact_static_actions", out runtime._static.ecsact_static_actions, runtime._static._availableMethods);
			LoadDelegate(lib, "ecsact_static_on_reload", out runtime._static.ecsact_static_on_reload, runtime._static._availableMethods);
			LoadDelegate(lib, "ecsact_static_off_reload", out runtime._static.ecsact_static_off_reload, runtime._static._availableMethods);

			// Load system implementation wasm methods
			LoadDelegate(lib, "ecsactsi_wasm_load", out runtime._wasm.ecsactsi_wasm_load, runtime._wasm._availableMethods);
			LoadDelegate(lib, "ecsactsi_wasm_load_file", out runtime._wasm.ecsactsi_wasm_load_file, runtime._wasm._availableMethods);
			LoadDelegate(lib, "ecsactsi_wasm_set_trap_handler", out runtime._wasm.ecsactsi_wasm_set_trap_handler, runtime._wasm._availableMethods);
		}

		if(runtime._wasm.ecsactsi_wasm_set_trap_handler != null) {
			runtime._wasm.ecsactsi_wasm_set_trap_handler(DefaultWasmTrapHandler);
		}

		return runtime;
	}

	public static void Free
		( EcsactRuntime runtime
		)
	{
		if(runtime._libs != null) {
			foreach(var lib in runtime._libs) {
				NativeLibrary.Free(lib);
			}

			runtime._libs = null;
		}
	}

	/// <summary>Init Component Untyped Callback</summary>
	private delegate void InitCompUtCb
		( Int32   entityId
		, object  component
		);

	/// <summary>Update Component Untyped Callback</summary>
	private delegate void UpCompUtCb
		( Int32   entityId
		, object  component
		);

	/// <summary>Remove Component Untyped Callback</summary>
	private delegate void RmvCompUtCb
		( Int32   entityId
		, object  component
		);

	private List<InitComponentCallback> _initAnyCompCbs = new();
	private List<UpdateComponentCallback> _updateAnyCompCbs = new();
	private List<RemoveComponentCallback> _removeAnyCompCbs = new();
	private Dictionary<Int32, List<InitCompUtCb>> _initCompCbs = new();
	private Dictionary<Int32, List<UpCompUtCb>> _updateCompCbs = new();
	private Dictionary<Int32, List<RmvCompUtCb>> _removeCompCbs = new();
	internal ExecutionEventsCollector _execEvs;

	private EcsactRuntime() {
		_execEvs = new ExecutionEventsCollector{
			initCallback = OnInitComponentHandler,
			initCallbackUserData = IntPtr.Zero,
			updateCallback = OnUpdateComponentHandler,
			updateCallbackUserData = IntPtr.Zero,
			removeCallback = OnRemoveComponentHandler,
			removeCallbackUserData = IntPtr.Zero,
		};
	}

	~EcsactRuntime() {
		Free(this);
	}

	public delegate void InitComponentCallback
		( Int32   entityId
		, Int32   componentId
		, object  component
		);

	public Action OnInitComponent
		( InitComponentCallback callback
		)
	{
		_initAnyCompCbs.Add(callback);
		return () => {
			_initAnyCompCbs.Remove(callback);
		};
	}

	public delegate void InitComponentCallback<ComponentT>
		( Int32       entityId
		, ComponentT  component
		) where ComponentT : Ecsact.Component;

	/// <summary>Adds a callback for when component init event is fired.</summary>
	/// <returns>Action that clears callback upon invocation.</returns>
	public Action OnInitComponent<ComponentT>
		( InitComponentCallback<ComponentT> callback
		) where ComponentT : Ecsact.Component
	{
		var compId = Ecsact.Util.GetComponentID<ComponentT>()!;

		if(!_initCompCbs.TryGetValue(compId, out var callbacks)) {
			callbacks = new();
			_initCompCbs.Add(compId, callbacks);
		}

		InitCompUtCb cb = (entityId, comp) => {
			callback(entityId, (ComponentT)comp);
		};

		callbacks.Add(cb);

		return () => {
			if(_initCompCbs.TryGetValue(compId, out var callbacks)) {
				callbacks.Remove(cb);
			}
		};
	}

	public delegate void UpdateComponentCallback
		( Int32   entityId
		, Int32   componentId
		, object  component
		);

	public Action OnUpdateComponent
		( UpdateComponentCallback callback
		)
	{
		_updateAnyCompCbs.Add(callback);
		return () => {
			_updateAnyCompCbs.Remove(callback);
		};
	}

	public delegate void UpdateComponentCallback<ComponentT>
		( Int32       entityId
		, ComponentT  component
		) where ComponentT : Ecsact.Component;

	public Action OnUpdateComponent<ComponentT>
		( UpdateComponentCallback<ComponentT> callback
		) where ComponentT : Ecsact.Component
	{
		var compId = Ecsact.Util.GetComponentID<ComponentT>()!;

		if(!_updateCompCbs.TryGetValue(compId, out var callbacks)) {
			callbacks = new();
			_updateCompCbs.Add(compId, callbacks);
		}

		UpCompUtCb cb = (entityId, comp) => {
			callback(entityId, (ComponentT)comp);
		};

		callbacks.Add(cb);

		return () => {
			if(_updateCompCbs.TryGetValue(compId, out var callbacks)) {
				callbacks.Remove(cb);
			}
		};
	}

	public delegate void RemoveComponentCallback
		( Int32   entityId
		, Int32   componentId
		, object  component
		);

	public Action OnRemoveComponent
		( RemoveComponentCallback callback
		)
	{
		_removeAnyCompCbs.Add(callback);
		return () => {
			_removeAnyCompCbs.Remove(callback);
		};
	}

	public delegate void RemoveComponentCallback<ComponentT>
		( Int32       entityId
		, ComponentT  component
		) where ComponentT : Ecsact.Component;

	public Action OnRemoveComponent<ComponentT>
		( RemoveComponentCallback<ComponentT> callback
		) where ComponentT : Ecsact.Component
	{
		var compId = Ecsact.Util.GetComponentID<ComponentT>()!;

		if(!_removeCompCbs.TryGetValue(compId, out var callbacks)) {
			callbacks = new();
			_removeCompCbs.Add(compId, callbacks);
		}

		RmvCompUtCb cb = (entityId, comp) => {
			callback(entityId, (ComponentT)comp);
		};

		callbacks.Add(cb);

		return () => {
			if(_removeCompCbs.TryGetValue(compId, out var callbacks)) {
				callbacks.Remove(cb);
			}
		};
	}


	[AOT.MonoPInvokeCallback(typeof(ComponentEventCallback))]
	private static void OnInitComponentHandler
		( EcsactEvent  ev
		, Int32        entityId
		, Int32        componentId
		, IntPtr       componentData
		, IntPtr       callbackUserData
		)
	{
		UnityEngine.Debug.Assert(ev == EcsactEvent.InitComponent);

		var self = (GCHandle.FromIntPtr(callbackUserData).Target as EcsactRuntime)!;
		var component = Ecsact.Util.PtrToComponent(componentData, componentId);

		if(self._initCompCbs.TryGetValue(componentId, out var cbs)) {
			foreach(var cb in cbs) {
				cb(entityId, component);
			}
		}

		foreach(var cb in self._initAnyCompCbs) {
			cb(entityId, componentId, component);
		}
	}

	[AOT.MonoPInvokeCallback(typeof(ComponentEventCallback))]
	private static void OnUpdateComponentHandler
		( EcsactEvent  ev
		, Int32        entityId
		, Int32        componentId
		, IntPtr       componentData
		, IntPtr       callbackUserData
		)
	{
		UnityEngine.Debug.Assert(ev == EcsactEvent.UpdateComponent);

		var self = (GCHandle.FromIntPtr(callbackUserData).Target as EcsactRuntime)!;
		var component = Ecsact.Util.PtrToComponent(componentData, componentId);

		if(self._updateCompCbs.TryGetValue(componentId, out var cbs)) {
			foreach(var cb in cbs) {
				cb(entityId, component);
			}
		}

		foreach(var cb in self._updateAnyCompCbs) {
			cb(entityId, componentId, component);
		}
	}

	[AOT.MonoPInvokeCallback(typeof(ComponentEventCallback))]
	private static void OnRemoveComponentHandler
		( EcsactEvent  ev
		, Int32        entityId
		, Int32        componentId
		, IntPtr       componentData
		, IntPtr       callbackUserData
		)
	{
		UnityEngine.Debug.Assert(ev == EcsactEvent.RemoveComponent);

		var self = (GCHandle.FromIntPtr(callbackUserData).Target as EcsactRuntime)!;
		var component = Ecsact.Util.PtrToComponent(componentData, componentId);

		if(self._removeCompCbs.TryGetValue(componentId, out var cbs)) {
			foreach(var cb in cbs) {
				cb(entityId, component);
			}
		}

		foreach(var cb in self._removeAnyCompCbs) {
			cb(entityId, componentId, component);
		}
	}

}
