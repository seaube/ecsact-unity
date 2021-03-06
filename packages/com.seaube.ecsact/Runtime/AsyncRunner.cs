using UnityEngine;

#nullable enable

namespace Ecsact {
	[AddComponentMenu("")]
	public class AsyncRunner : MonoBehaviour {
		private static AsyncRunner? instance = null;
		private EcsactRuntime? runtimeInstance = null;

		[RuntimeInitializeOnLoadMethod]
		private static void OnRuntimeLoad() {
			if(instance != null) {
				return;
			}

			var settings = EcsactRuntimeSettings.Get();
			if(!settings.useAsyncRunner) {
				return;
			}

			var gameObject = new GameObject("Ecsact Async Runner");
			instance = gameObject.AddComponent<AsyncRunner>();
			DontDestroyOnLoad(gameObject);
		}

		void Awake() {
			runtimeInstance = EcsactRuntime.GetOrLoadDefault();
		}

		void Update() {
			runtimeInstance!.async.FlushEvents();
		}

		void OnDestroy() {
			if(this.Equals(instance)) {
				instance = null;
			}
		}
	}
}
