﻿//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace ScreenTime {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.10.0.0")]
    internal sealed partial class DefaultSettings : global::System.Configuration.ApplicationSettingsBase {
        
        private static DefaultSettings defaultInstance = ((DefaultSettings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new DefaultSettings())));
        
        public static DefaultSettings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1")]
        public int GetTopWindowInterval_s {
            get {
                return ((int)(this["GetTopWindowInterval_s"]));
            }
            set {
                this["GetTopWindowInterval_s"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("3")]
        public int RefreshListBoxInterval_s {
            get {
                return ((int)(this["RefreshListBoxInterval_s"]));
            }
            set {
                this["RefreshListBoxInterval_s"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool HideWhenStart {
            get {
                return ((bool)(this["HideWhenStart"]));
            }
            set {
                this["HideWhenStart"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute(".\\data")]
        public string UserDataDirectory {
            get {
                return ((string)(this["UserDataDirectory"]));
            }
            set {
                this["UserDataDirectory"] = value;
            }
        }
    }
}
