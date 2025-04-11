using System;
using System.IO;
using System.Reflection;

namespace LogicAppUnit.Helper
{
    /// <summary>
    /// Helper class to read embedded resources from an assembly.
    /// </summary>
    public static class ResourceHelper
    {
        /// <summary>
        /// Get an assembly resource <strong>from the calling assembly</strong> as a <see cref="Stream"/>.
        /// </summary>
        /// <param name="resourceName">The fully-qualified name of the resource.</param>
        /// <returns>The resource data.</returns>
        public static Stream GetAssemblyResourceAsStream(string resourceName)
        {
            return GetAssemblyResourceAsStream(resourceName, Assembly.GetCallingAssembly());
        }

        /// <summary>
        /// Get an assembly resource as a <see cref="Stream"/>.
        /// </summary>
        /// <param name="resourceName">The fully-qualified name of the resource.</param>
        /// <param name="containingAssembly">The assembly containing the resource.</param>
        /// <returns>The resource data.</returns>
        public static Stream GetAssemblyResourceAsStream(string resourceName, Assembly containingAssembly)
        {
            ArgumentNullException.ThrowIfNull(resourceName);
            ArgumentNullException.ThrowIfNull(containingAssembly);

            Stream resourceData = containingAssembly.GetManifestResourceStream(resourceName);
            if (resourceData == null)
                throw new TestException($"The resource '{resourceName}' could not be found in assembly '{containingAssembly.GetName().Name}'. Make sure that the resource name is a fully qualified name (including the .NET namespace), that the correct assembly is referenced and the resource is built as an Embedded Resource.");

            return resourceData;
        }

        /// <summary>
        /// Get an assembly resource <strong>from the calling assembly</strong> as a <see cref="string"/> value.
        /// </summary>
        /// <param name="resourceName">The fully-qualified name of the resource.</param>
        /// <returns>The resource data.</returns>
        public static string GetAssemblyResourceAsString(string resourceName)
        {
            return ContentHelper.ConvertStreamToString(GetAssemblyResourceAsStream(resourceName, Assembly.GetCallingAssembly()));
        }

        /// <summary>
        /// Get an assembly resource as a <see cref="string"/> value.
        /// </summary>
        /// <param name="resourceName">The fully-qualified name of the resource.</param>
        /// <param name="containingAssembly">The assembly containing the resource.</param>
        /// <returns>The resource data.</returns>
        public static string GetAssemblyResourceAsString(string resourceName, Assembly containingAssembly)
        {
            return ContentHelper.ConvertStreamToString(GetAssemblyResourceAsStream(resourceName, containingAssembly));
        }
    }
}
