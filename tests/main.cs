// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using shaderc;

namespace tests {
	public class main {

		static void compile (Compiler comp, string path, ShaderKind shaderKind) {
			using (Result res = comp.Compile (path, shaderKind)) {
				Console.WriteLine ($"{path}: {res.Status}");
				if (res.Status != Status.Success) {
					Console.WriteLine ($"\terrs:{res.ErrorCount} warns:{res.WarningCount}");
					Console.WriteLine ($"\t{res.ErrorMessage}");

				}
			}
		}

		static void Main (string[] args) {


			Compiler.GetSpvVersion (out SpirVVersion version, out uint revision);
			Console.WriteLine ($"SpirV: version={version} revision={revision}");


			using (Compiler comp = new Compiler ()) {	
					
				compile (comp, @"shaders/debug.vert", ShaderKind.VertexShader);
				compile (comp, @"shaders/debug.frag", ShaderKind.FragmentShader);

				comp.Options.IncludeDirectories.Add ("shaders");
				compile (comp, @"shaders/deferred/GBuffPbr.frag", ShaderKind.FragmentShader);
			}
		}
	}
}
