using Android.App;
using Android.Runtime;

namespace DisplayApp.Platforms.Android
{
    /// <summary>
    /// Represents the Android application entry point for the MAUI app.
    /// This class is required by the Android runtime and is decorated with the <see cref="ApplicationAttribute"/>.
    /// </summary>
    [Application]
    public class MainApplication : MauiApplication
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainApplication"/> class.
        /// </summary>
        /// <param name="handle">
        /// The JNI handle of the native Android application instance.
        /// </param>
        /// <param name="ownership">
        /// Specifies the ownership rules for the JNI handle.
        /// </param>
        public MainApplication(nint handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        /// <summary>
        /// Creates the MAUI application instance.
        /// </summary>
        /// <returns>
        /// A <see cref="MauiApp"/> instance representing the MAUI app.
        /// </returns>
        /// <remarks>
        /// This method is called by the MAUI framework to bootstrap the application.
        /// It should return the app created by <c>MauiProgram.CreateMauiApp()</c>.
        /// </remarks>
        protected override MauiApp CreateMauiApp()
        {
            return MauiProgram.CreateMauiApp();
        }
    }
}
