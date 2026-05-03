using System.Runtime.CompilerServices;
using Il2CppInterop.Common;
using Il2CppSystem;

namespace Il2CppInterop.Runtime.Extensions;

internal static class Il2CppSystemTypeExtensions
{
    extension(Type type)
    {
        public static Type FromTypePointer(nint typePointer)
        {
            // Ensure Il2CppSystem.RuntimeType is initialized before we call Il2CppSystem.Type.internal_from_handle
            RuntimeHelpers.RunClassConstructor(typeof(RuntimeType).TypeHandle);

            return Type.internal_from_handle(typePointer);
        }

        public static Type FromClassPointer(nint classPointer)
        {
            var il2CppType = IL2CPP.il2cpp_class_get_type(classPointer);
            if (il2CppType == default)
            {
                throw new System.ArgumentException($"Class pointer {classPointer} does not have a corresponding IL2CPP type pointer", nameof(classPointer));
            }
            return Type.FromTypePointer(il2CppType);
        }

        public nint ToTypePointer()
        {
            return type.TypeHandle.value;
        }

        /// <summary>
        /// Get the class pointer for this type
        /// </summary>
        /// <remarks>
        /// This can be null if the type doesn't have a corresponding class, such as generic type instance not used in the game.
        /// </remarks>
        /// <returns>The class pointer for this type</returns>
        public nint ToClassPointer()
        {
            return IL2CPP.il2cpp_class_from_type(type.ToTypePointer());
        }
    }
}
