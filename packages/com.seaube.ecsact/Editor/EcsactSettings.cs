using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
class EcsactSettings : ScriptableObject {
	public const string assetPath = "Assets/Editor/EcsactSettings.asset";
	public const string path = "Project/EcsactProjectSettings";
	public const SettingsScope scope = SettingsScope.Project;

	/// <summary>Path to <c>ecsact_parser_info_codegen.exe</c>. If unset
	/// the latest will be downloaded and used instead.</summary>
	public string parseInfoCodegenPluginPath = "";

	/// <summary>Path to <c>ecsact_csharp_codegen.exe</c>. If unset the latest
	/// will be downloaded and used instead.</summary>
	public string csharpCodegenPluginPath = "";

	internal static EcsactSettings GetOrCreateSettings() {
		var settings = AssetDatabase.LoadAssetAtPath<EcsactSettings>(assetPath);
		if(settings == null) {
			settings = ScriptableObject.CreateInstance<EcsactSettings>();
			AssetDatabase.CreateAsset(settings, assetPath);
			AssetDatabase.SaveAssetIfDirty(settings);
		}

		return settings;
	}

	internal static SerializedObject GetSerializedSettings() {
		return new SerializedObject(GetOrCreateSettings());
	}
}

static class EcsactSettingsUIElementsRegister {
	[SettingsProvider]
	public static SettingsProvider CreateEcsactSettingsProvider() {
		return new SettingsProvider(EcsactSettings.path, EcsactSettings.scope) {
			label = "Ecsact",
			activateHandler = (searchContext, rootElement) => {
				var settings = EcsactSettings.GetSerializedSettings();
				var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
					"Packages/com.seaube.ecsact/Editor/EcsactSettings.uxml"
				);
				var ui = template.Instantiate();
				BindingExtensions.Bind(ui, settings);
				rootElement.Add(ui);
			},
			keywords = new HashSet<string>(new[] {
				"ECSACT",
				"ECSACT",
				"ECS",
				"ECS Plugin",
				"Plugin",
				"Runtime",
				"Library",
			}),
		};
	}
}
