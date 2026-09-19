"""Invoke an isolated managed check using the installed Valheim Unity Mono runtime.

No game process is launched. No installed files are changed or copied. Run only
trusted probe assemblies: this executes their code with the current user's access.
"""
import argparse
import ctypes
import os
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("assembly", type=Path)
    parser.add_argument("--game", type=Path, default=Path(r"C:\Program Files (x86)\Steam\steamapps\common\Valheim"))
    parser.add_argument("--namespace", default="ValheimSagas.Incident")
    parser.add_argument("--class-name", default="MonoEntry")
    parser.add_argument("--method", default="Run")
    args = parser.parse_args()
    assembly = args.assembly.resolve(strict=True)
    managed = (args.game / "valheim_Data/Managed").resolve(strict=True)
    runtime = (args.game / "MonoBleedingEdge/EmbedRuntime").resolve(strict=True)
    config = (args.game / "MonoBleedingEdge/etc").resolve(strict=True)
    os.environ["MONO_PATH"] = str(assembly.parent) + os.pathsep + str(managed)
    dll_dir = os.add_dll_directory(str(runtime))
    mono = ctypes.CDLL(str(runtime / "mono-2.0-bdwgc.dll"))
    ptr = ctypes.c_void_p
    char = ctypes.c_char_p

    def api(name, result, *parameters):
        function = getattr(mono, name)
        function.restype = result
        function.argtypes = parameters
        return function

    api("mono_set_dirs", None, char, char)(str(managed).encode(), str(config).encode())
    api("mono_set_assemblies_path", None, char)(os.environ["MONO_PATH"].encode())
    api("mono_config_parse", None, char)(None)
    domain = api("mono_jit_init_version", ptr, char, char)(b"ValheimSagasIsolatedProbe", b"v4.0.30319")
    if not domain:
        raise RuntimeError("Mono initialization failed")
    # Unity normally supplies an application configuration path. A foreign
    # embedding host must provide one before Mono's HTTP/configuration APIs run.
    host_config = assembly.parent / "isolated-mono-host.config"
    host_config.write_text("<configuration />", encoding="utf-8")
    api("mono_domain_set_config", None, ptr, char, char)(domain, str(assembly.parent).encode(), str(host_config).encode())
    loaded = api("mono_domain_assembly_open", ptr, ptr, char)(domain, str(assembly).encode())
    if not loaded:
        raise RuntimeError("Mono could not load probe assembly")
    image = api("mono_assembly_get_image", ptr, ptr)(loaded)
    klass = api("mono_class_from_name", ptr, ptr, char, char)(image, args.namespace.encode(), args.class_name.encode())
    if not klass:
        raise RuntimeError("Probe entry class not found")
    method = api("mono_class_get_method_from_name", ptr, ptr, char, ctypes.c_int)(klass, args.method.encode(), 0)
    if not method:
        raise RuntimeError("Probe zero-argument entry method not found")
    exception = ptr()
    print("Runtime:", runtime / "mono-2.0-bdwgc.dll", flush=True)
    print("Probe:", assembly, flush=True)
    api("mono_runtime_invoke", ptr, ptr, ptr, ptr, ctypes.POINTER(ptr))(method, None, None, ctypes.byref(exception))
    if exception.value:
        api("mono_print_unhandled_exception", None, ptr)(exception)
        raise RuntimeError("Probe threw a managed exception (see Mono output)")
    print("Managed entry completed successfully.", flush=True)
    # Deliberately let process exit unload Mono: jit_cleanup can race runtime
    # background threads inside a foreign (Python) host.
    dll_dir.close()


if __name__ == "__main__":
    main()
