// Copyright (c) 2013-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace shaderc {
	public class Compiler : IDisposable {
		IntPtr handle;
		public Options Options { get; private set; }

		public Compiler () {
			handle = NativeMethods.shaderc_compiler_initialize ();
			if (handle == IntPtr.Zero)
				throw new Exception ("error");
			Options = new Options ();
		}


		/// <summary>
		/// Compile the specified path, shaderKind and entry_point.
		/// </summary>
		/// <returns>The compile.</returns>
		/// <param name="path">Full path of the shader source file</param>
		/// <param name="shaderKind">If the shader kind is not set to a specified kind, but shaderc_glslc_infer_from_source,
		/// the compiler will try to deduce the shader kind from the source string and a failure in deducing will generate an error. Currently only
		/// #pragma annotation is supported. If the shader kind is set to one of the default shader kinds, the compiler will fall back to the default shader
		/// kind in case it failed to deduce the shader kind from source string.</param>
		/// <param name="entry_point">Entry point.</param>
		public Result Compile (string path, ShaderKind shaderKind, string entry_point = "main") {
			if (!File.Exists (path))
				throw new FileNotFoundException ("spirv file not found", path);
			string source = "";
			using (StreamReader sr = new StreamReader (path)) 
				source = sr.ReadToEnd ();
			return Compile (source, path, shaderKind, entry_point);
		}

		/// <summary>
		/// Takes a GLSL source string and the associated shader kind, input file
		/// name, compiles it according to the given additional_options. 
		/// If the additional_options parameter is not null, then the compilation is modified by any options
		/// present.  May be safely called from multiple threads without explicit synchronization.
		/// If there was failure in allocating the compiler object, null will be returned.
		/// </summary>
		/// <returns>Compilation result</returns>
		/// <param name="source">the source code plain text</param>
		/// <param name="fileName">used as a tag to identify the source string in cases like emitting error messages. It
		/// doesn't have to be a 'file name'.</param>
		/// <param name="shaderKind">If the shader kind is not set to a specified kind, but shaderc_glslc_infer_from_source,
		/// the compiler will try to deduce the shader kind from the source string and a failure in deducing will generate an error. Currently only
		/// #pragma annotation is supported. If the shader kind is set to one of the default shader kinds, the compiler will fall back to the default shader
		/// kind in case it failed to deduce the shader kind from source string.</param>
		/// <param name="entry_point">defines the name of the entry point to associate with this GLSL source.</param>
		public Result Compile (string source, string fileName, ShaderKind shaderKind, string entry_point = "main") {
			return new Result (NativeMethods.shaderc_compile_into_spv (handle, source, (ulong)source.Length, (byte)shaderKind, fileName, "main", Options.handle));
		}

		#region IDisposable implementation
		public void Dispose () {
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing) {
			if (disposing)
				Options.Dispose ();
			if (!disposing || handle == IntPtr.Zero)
				return;

			NativeMethods.shaderc_compiler_release (handle);
			handle = IntPtr.Zero;
		}
		#endregion	
	}
}
