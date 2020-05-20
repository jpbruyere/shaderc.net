
<p align="center">
  <a href="https://www.nuget.org/packages/shaderc.net"><img src="https://buildstats.info/nuget/shaderc.net"></a>
  <a href="https://travis-ci.org/jpbruyere/shaderc.net">
      <img src="https://img.shields.io/travis/jpbruyere/shaderc.net.svg?&logo=travis&logoColor=white">
  </a>
  <a href="https://ci.appveyor.com/project/jpbruyere/shaderc-net">
    <img src="https://img.shields.io/appveyor/ci/jpbruyere/shaderc-net?logo=appveyor&logoColor=lightgrey">
  </a>
  <a href="https://www.paypal.me/GrandTetraSoftware">
    <img src="https://img.shields.io/badge/Donate-PayPal-green.svg">
  </a>
</p>

# shaderc.net

Net bindings for [google shaderc](https://github.com/google/shaderc).

#### spirv compilation
This sample use [vk.net](https://github.com/jpbruyere/vk.net) to create the shader module.
On success, `Result` object will hold a native pointer on the generated spirv code suitable for the `ShaderModuleCreateInfo` pCode field. This pointer will stay valid until the `Result` disposal.

```csharp
using (Compiler comp = new Compiler ()) {
  using (Result res = comp.Compile ("test.vert", ShaderKind.VertexShader)) {
    if (res.Status == Status.Success) {
      VkShaderModuleCreateInfo ci = VkShaderModuleCreateInfo.New ();
      ci.codeSize = res.codeSize;
      ci.pCode = res.code;
      vkCreateShaderModule (VkDev, ref moduleCreateInfo, IntPtr.Zero, out VkShaderModule shaderModule));
```

#### Resolving includes
**shaderc** library provide the ability to add `#include` statements as in **c/c++**. This functionality is enabled or not in the `Options` class constructor, the default is enabled.
```csharp
Options opt = new Options(false);
```
A default `Options` instance is created by the `Compiler` constructor which enable the include resolution. You may provide a custom Options instance to the compiler constructor.
```csharp
Compiler comp = new Compiler (opt);
comp.Options.InvertY = true;
```
As in **c/c++**, you may have local or global include (enclosed in "" or <>). Local includes enclosed in "" will be searched from the current parsed source file. Global includes enclosed in '<>' will be searched in directories listed in ```Options.IncludeDirectories```. The pathes may be relative to the executable directory, or absolute.
```csharp
comp.Options.IncludeDirectories.AddRange ("shaders", @"c:\test");
```
If you want to override the default include resolution, to search for embedded ressources for example, derive the `Options` class and override the `TryFindInclude` method.
```csharp
class OptionsWithCustomIncResolve : Options {
  protected override bool TryFindInclude (string sourcePath, string includePath, IncludeType incType, out string incFile, out string incContent) {
...
```


