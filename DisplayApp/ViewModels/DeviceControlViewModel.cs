using System.ComponentModel;
using System.Runtime.CompilerServices;
using DisplayLogic.Models;

namespace DisplayApp.ViewModels
{
    /// <summary>
    /// ViewModel for the <see cref="DeviceControlPage"/>.
    /// Manages the state of a single <see cref="MqttDevice"/> and provides property change notifications for data binding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements <see cref="INotifyPropertyChanged"/> to support two-way data binding in MAUI.
    /// When properties change, UI elements bound to them automatically update.
    /// </para>
    /// <para>
    /// This ViewModel is designed to wrap a single <see cref="MqttDevice"/> instance and expose it
    /// to the view for display and interaction. Any changes to the <see cref="Device"/> property
    /// trigger property change notifications.
    /// </para>
    /// </remarks>
    public partial class DeviceControlViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Backing field for the <see cref="Device"/> property.
        /// Required during initialization.
        /// </summary>
        public required MqttDevice _device;

        /// <summary>
        /// The <see cref="MqttDevice"/> instance currently being managed by this ViewModel.
        /// </summary>
        /// <remarks>
        /// Setting this property updates the underlying device reference and raises <see cref="PropertyChanged"/>
        /// to notify any bound UI elements of the change.
        /// </remarks>
        public MqttDevice Device
        {
            get => _device;
            set
            {
                if (_device != value)
                {
                    _device = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Event that is raised when a property value changes.
        /// Part of the <see cref="INotifyPropertyChanged"/> implementation for data binding.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property.
        /// </summary>
        /// <param name="propertyName">
        /// Name of the property that changed. Automatically provided by the compiler using <see cref="CallerMemberName"/>.
        /// </param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
