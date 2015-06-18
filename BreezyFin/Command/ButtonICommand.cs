using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;


namespace Breezy.Fin.Command
{
    public class ButtonICommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private readonly Action _actionExecute;
        private readonly Func<bool> _funcCanExecute;

        public ButtonICommand(Action what, Func<bool> when)
        {
            _actionExecute = what;
            _funcCanExecute = when;
        }

        public bool CanExecute(object parameter)
        {
            return _funcCanExecute();
        }

        public void Execute(object parameter)
        {
            _actionExecute();
        }

        public void StubCanExecuteChanged()
        {
            if (CanExecuteChanged != null)
                CanExecuteChanged(this, new EventArgs());
        }
    }
}
