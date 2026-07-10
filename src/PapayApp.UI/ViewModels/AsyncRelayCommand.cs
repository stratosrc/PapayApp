using System.Windows.Input;

namespace PapayApp.UI.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !_isRunning;
    }

    public async void Execute(object? parameter)
    {
        if (_isRunning)
        {
            return;
        }

        try
        {
            _isRunning = true;
            RaiseCanExecuteChanged();
            await _execute();
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
