using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AppUsageTimer
{
    public class ProcessViewModel : INotifyPropertyChanged
    {
        private string _processName = string.Empty;
        private TimeSpan _totalTime;
        private TimeSpan _sessionTime;

        public string ProcessName
        {
            get => _processName;
            set => SetField(ref _processName, value);
        }

        public TimeSpan TotalTime
        {
            get => _totalTime;
            set
            {
                if (SetField(ref _totalTime, value))
                {
                    OnPropertyChanged(nameof(FormattedTotalTime));
                }
            }
        }

        public TimeSpan SessionTime
        {
            get => _sessionTime;
            set
            {
                if (SetField(ref _sessionTime, value))
                {
                    OnPropertyChanged(nameof(FormattedSessionTime));
                }
            }
        }

        public string FormattedTotalTime => TotalTime.ToString(@"dd\.hh\:mm\:ss");

        public string FormattedSessionTime => SessionTime.ToString(@"dd\.hh\:mm\:ss");


        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public ProcessViewModel(string processName, TimeSpan totalTime, TimeSpan sessionTime)
        {
            _processName = processName;
            _totalTime = totalTime;
            _sessionTime = sessionTime;
        }

        public ProcessViewModel(string processName)
        {
            _processName = processName;
            _totalTime = TimeSpan.Zero;
            _sessionTime = TimeSpan.Zero;
        }
    }
}