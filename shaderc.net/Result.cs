// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Runtime.InteropServices;

namespace shaderc {
	public class Result : IDisposable {
		internal IntPtr handle;

		internal Result (IntPtr handle) {
			this.handle = handle;
			if (handle == IntPtr.Zero)
				throw new Exception ("error");
		}
		/// <summary>
		/// Returns the compilation status, indicating whether the compilation succeeded,
		/// or failed due to some reasons, like invalid shader stage or compilation
		/// errors.
		/// </summary>
		public Status Status => NativeMethods.shaderc_result_get_compilation_status (handle);
		/// <summary>
		/// Returns the number of errors generated during the compilation.
		/// </summary>
		public uint ErrorCount => (uint)NativeMethods.shaderc_result_get_num_errors (handle);
		/// <summary>
		/// // Returns the number of warnings generated during the compilation.
		/// </summary>
		public uint WarningCount => (uint)NativeMethods.shaderc_result_get_num_warnings (handle);
		/// <summary>
		/// Returns a null-terminated string that contains any error messages generated
		/// during the compilation.
		/// </summary>
		public string ErrorMessage =>
				Marshal.PtrToStringAnsi(NativeMethods.shaderc_result_get_error_message (handle));
			
		/// <summary>
		/// Returns a pointer to the start of the compilation output data bytes, either
		/// SPIR-V binary or char string. When the source string is compiled into SPIR-V
		/// binary, this is guaranteed to be castable to a uint32_t*. If the result
		/// contains assembly text or preprocessed source text, the pointer will point to
		/// the resulting array of characters.
		/// </summary>
		public IntPtr CodePointer => NativeMethods.shaderc_result_get_bytes (handle);
		/// <summary>
		/// Returns the number of bytes of the compilation output data in a result object.
		/// </summary>
		public uint CodeLength => (uint)NativeMethods.shaderc_result_get_length (handle);

		#region IDisposable implementation
		public void Dispose () {
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing) {
			if (handle == IntPtr.Zero)
				return;
			if (!disposing)
				Console.WriteLine ("[shaderc]Result disposed by finalyser");

			NativeMethods.shaderc_result_release (handle);
			handle = IntPtr.Zero;
		}
		#endregion
	}
}
