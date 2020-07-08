using System;
using System.Windows.Controls;
using Hallupa.Library;

namespace StrategyRunnerLive.ViewModels
{
    public class LoginViewModel
    {
        private readonly Action _closeViewAction;
        private readonly Action<string, string, string> _loginAction;

        public LoginViewModel(Action closeViewAction, Action<string, string, string> loginAction)
        {
            Username = Settings.Default.Username;

            _closeViewAction = closeViewAction;
            _loginAction = loginAction;
            LoginCommand = new DelegateCommand(Login);
        }

        public DelegateCommand LoginCommand { get; }

        public string Username { get; set; }

        public string Connection { get; set; } = "GBREAL";

        private void Login(object obj)
        {
            var passwordBox = (PasswordBox)obj;
            var password = passwordBox.Password;
            _closeViewAction();

            Settings.Default.Username = Username;
            Settings.Default.Save();

            _loginAction(Username, password, Connection);
        }
    }
}