// Copyright (c) 2013-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace shaderc {
	public class IncludeResolutionEventArgs : EventArgs {
		public readonly string Source;
		public readonly string Include;
		public readonly IncludeType Type;
		public string ResolvedName;
		public string ResolvedContent;

		internal IncludeResolutionEventArgs (string source, string include, IncludeType type) {
			Source = source;
			Include = include;
			Type = type;
		}
	}
	public class Options : IDisposable, ICloneable {
		internal IntPtr handle;
		static int curId;//id counter
		readonly internal int id;//dic key
		readonly internal bool includeEnabled;

		static internal Dictionary<int, Options> optionsDic = new Dictionary<int, Options> ();//context data for callbacks

		/// <summary>
		/// List of the pathes to search when trying to resolve a 'Standard' include (enclosed in $lt;>)
		/// May be absolute pathes or relative to the executable directory.
		/// </summary>
		public readonly List<string> IncludeDirectories = new List<string> ();

		/// <summary>
		/// Create a new instance of the Options class.
		/// </summary>
		/// <param name="enableIncludes">If set to 'true' include resolution is activated</param>
		public Options (bool enableIncludes = true) : this (NativeMethods.shaderc_compile_options_initialize (), enableIncludes) { }

		Options (IntPtr handle, bool enableIncludes) {
			this.handle = handle;
			if (handle == IntPtr.Zero)
				throw new Exception ("error");
			id = curId++;
			optionsDic.Add (id, this);
			includeEnabled = enableIncludes;
			if (enableIncludes)
				setIncludeCallbacks ();
		}

		#region includ result handling
		static internal PFN_IncludeResolve resolve = HandlePFN_IncludeResolve;
		static internal PFN_IncludeResultRelease incResultRelease = HandlePFN_IncludeResultRelease;

		/// <summary>
		/// Include resolution method. Override it to provide a custom include resolution. Note that
		/// this Options instance must have been created with enableIncludes set to 'true'.
		/// </summary>
		/// <returns><c>true</c>, if find include was tryed, <c>false</c> otherwise.</returns>
		/// <param name="source">requesting source name</param>
		/// <param name="include">include name to search for.</param>
		/// <param name="incType">As in c, relative include or global</param>
		/// <param name="incFile">the resolved name of the include, empty if resolution failed</param>
		/// <param name="incContent">if resolution succeeded, contain the source code in plain text of the include</param>
		protected virtual bool TryFindInclude (string source, string include, IncludeType incType, out string incFile, out string incContent) {
			if (incType == IncludeType.Relative) {
				incFile = Path.Combine (Path.GetDirectoryName(source), include);
				if (File.Exists (incFile)) {
					using (StreamReader sr = new StreamReader (incFile))
						incContent = sr.ReadToEnd ();
					return true;
				}

			} else {
				foreach (string incDir in IncludeDirectories) {
					incFile = Path.Combine (incDir, include);
					if (File.Exists (incFile)) {
						using (StreamReader sr = new StreamReader (incFile))
							incContent = sr.ReadToEnd ();
						return true;
					}
				}
			}
			
			incFile = "";
			incContent = "";
			return false;
		}


		static IntPtr HandlePFN_IncludeResolve (IntPtr userData, string requestedSource, int type, string requestingSource, UIntPtr includeDepth) {

			Options opts = optionsDic[userData.ToInt32 ()];
			string content = "", incFile = "";

			if (opts.TryFindInclude (requestingSource, requestedSource, (IncludeType)type, out incFile, out content))
				using (StreamReader sr = new StreamReader (incFile))
					content = sr.ReadToEnd ();

			IncludeResult result = new IncludeResult (incFile, content, userData.ToInt32());
			IntPtr irPtr = Marshal.AllocHGlobal (Marshal.SizeOf<IncludeResult> ());
			Marshal.StructureToPtr (result, irPtr, true);
			return irPtr;
		}
		static void HandlePFN_IncludeResultRelease (IntPtr userData, IntPtr includeResult) {
			Marshal.PtrToStructure<IncludeResult> (includeResult).FreeStrings ();
			Marshal.FreeHGlobal (includeResult);
		}
		void setIncludeCallbacks () {
			NativeMethods.shaderc_compile_options_set_include_callbacks (handle, Marshal.GetFunctionPointerForDelegate (resolve),
				Marshal.GetFunctionPointerForDelegate (incResultRelease), (IntPtr)id);
		}
		#endregion


		/// <summary>
		/// Returns a copy of the given shaderc Options.
		/// </summary>
		/// <returns>The clone.</returns>
		public object Clone () => new Options (NativeMethods.shaderc_compile_options_clone (handle), includeEnabled);
		/// <summary>
		/// Adds a predefined macro to the compilation options. This has the same
		/// effect as passing -Dname=value to the command-line compiler.
		/// </summary>
		/// <remarks>
		/// If value is NULL, it has the same effect as passing -Dname to the command-line
		/// compiler. If a macro definition with the same name has previously been
		/// added, the value is replaced with the new value. The macro name and
		/// value are passed in with char pointers, which point to their data, and
		/// the lengths of their data. The strings that the name and value pointers
		/// point to must remain valid for the duration of the call, but can be
		/// modified or deleted after this function has returned. In case of adding
		/// a valueless macro, the value argument should be a null pointer or the
		/// value_length should be 0u.
		/// </remarks>
		/// <param name="name">Name.</param>
		/// <param name="value">Value.</param>
		public void AddMacroDefinition (string name, string value = null) => NativeMethods.shaderc_compile_options_add_macro_definition (
			handle, name, (ulong)name.Length, value, string.IsNullOrEmpty(value) ? 0 : (ulong)value.Length);
		/// <summary>
		/// Sets the source language.  The default is GLSL.
		/// </summary>
		public SourceLanguage SourceLanguage { set => NativeMethods.shaderc_compile_options_set_source_language (handle, value); }
		/// <summary>
		/// Sets the compiler optimization level to the given level.
		/// </summary>
		public OptimizationLevel Optimization { set => NativeMethods.shaderc_compile_options_set_optimization_level (handle, value); }
		/// <summary>
		/// Sets the compiler mode to generate debug information in the output.
		/// </summary>
		public void SetDebugInfoOn () => NativeMethods.shaderc_compile_options_set_generate_debug_info (handle);
		/// <summary>
		/// Sets the compiler mode to suppress warnings, overriding warnings-as-errors
		/// mode. When both suppress-warnings and warnings-as-errors modes are
		/// turned on, warning messages will be inhibited, and will not be emitted
		/// as error messages.
		/// </summary>
		public void SetSuppressWarnings () => NativeMethods.shaderc_compile_options_set_suppress_warnings (handle);
		/// <summary>
		/// Sets whether the compiler should invert position.Y output in vertex shader.
		/// </summary>
		public bool InvertY { set => NativeMethods.shaderc_compile_options_set_invert_y (handle, value); }


		#region IDisposable implementation
		public void Dispose () {
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing) {
			if (!disposing || handle == IntPtr.Zero)
				return;
			optionsDic.Remove (id);
			NativeMethods.shaderc_compile_options_release (handle);
			handle = IntPtr.Zero;
		}
		#endregion
	}
}
