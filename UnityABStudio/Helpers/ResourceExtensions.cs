namespace SoarCraft.QYun.UnityABStudio.Helpers {
    using Microsoft.ApplicationModel.Resources;

    internal static class ResourceExtensions {
        private static ResourceLoader _resLoader = new();

        public static string GetLocalized(this string resourceKey) {
            return _resLoader.GetString(resourceKey);
        }
    }
}
