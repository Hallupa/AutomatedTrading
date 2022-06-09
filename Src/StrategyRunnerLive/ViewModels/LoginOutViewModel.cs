using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Hallupa.Library;
using log4net;
using StrategyRunnerLive.Views;
using TraderTools.Basics;
using TraderTools.Brokers.FXCM;

namespace StrategyRunnerLive.ViewModels
{
    public class LoginOutViewModel : INotifyPropertyChanged
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string _loginOutButtonText;
        private bool _loginOutButtonEnabled = true;
        private readonly FxcmBroker _fxcm;
        private Action<Action<string, string, string>> _createLoginViewFunc;
        private Func<(Action<string> show, Action<string> updateText, Action close)> _createProgressingViewFunc;
        private Dispatcher _dispatcher;

        [Import] private IBrokersService _brokersService;
        [Import] private IMarketDetailsService _marketsService;

        public LoginOutViewModel()
        {
            DependencyContainer.ComposeParts(this);

            LoginOutCommand = new DelegateCommand(o => LoginOut(), o => LoginOutButtonEnabled);
            _loginOutButtonText = "Login";
            _fxcm = (FxcmBroker)_brokersService.Brokers.First(x => x.Name == "FXCM");
            _dispatcher = Dispatcher.CurrentDispatcher;

            _createLoginViewFunc = loginAction =>
            {
                var view = new LoginView { Owner = Application.Current.MainWindow };
                var loginVm = new LoginViewModel(() => view.Close(), loginAction);
                view.DataContext = loginVm;
                view.ShowDialog();
            };

            _createProgressingViewFunc = () =>
            {
                var view = new ProgressView { Owner = Application.Current.MainWindow };

                return (text =>
                    {
                        view.TextToShow.Text = text;
                        view.ShowDialog();
                    },
                    txt =>
                    {
                        if (_dispatcher.CheckAccess())
                        {
                            view.TextToShow.Text = txt;
                        }
                        else
                        {
                            _dispatcher.BeginInvoke((Action)(() => { view.TextToShow.Text = txt; }));
                        }
                    },
                    () => view.Close());
            };
        }

        public string LoginOutButtonText
        {
            get => _loginOutButtonText;
            set
            {
                _loginOutButtonText = value;
                OnPropertyChanged();
            }
        }

        public bool LoginOutButtonEnabled
        {
            get => _loginOutButtonEnabled;
            set
            {
                _loginOutButtonEnabled = value;
                LoginOutCommand.RaiseCanExecuteChanged();
                OnPropertyChanged();
            }
        }

        public DelegateCommand LoginOutCommand { get; }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void LoginOut()
        {
            LoginOutButtonEnabled = false;
            var loginAttempted = false;

            if (_fxcm.Status != ConnectStatus.Connected)
            {
                var progressViewActions = _createProgressingViewFunc();

                _createLoginViewFunc((username, password, connection) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            loginAttempted = true;
                            _fxcm.SetUsernamePassword(username, password, connection);
                            _fxcm.Connect();

                            if (_fxcm.Status == ConnectStatus.Connected)
                            {
                                var marketDetailsList = _fxcm.GetMarketDetailsList();
                                if (marketDetailsList != null)
                                {
                                    foreach (var marketDetails in marketDetailsList)
                                    {
                                        _marketsService.AddMarketDetails(marketDetails);
                                    }

                                    _marketsService.SaveMarketDetailsList();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Unable to login", ex);
                        }

                        _dispatcher.Invoke(() =>
                        {
                            progressViewActions.close();
                        });
                    });

                    progressViewActions.show("Logging in...");
                });
            }
            else
            {
                var progressViewActions = _createProgressingViewFunc();

                Task.Run(() =>
                {
                    _fxcm.Disconnect();

                    _dispatcher.Invoke(() =>
                    {
                        progressViewActions.close();
                        LoginOutButtonEnabled = true;
                        UpdateLoginButtonText();
                    });
                });

                progressViewActions.show("Logging out...");
            }

            LoginOutButtonEnabled = true;

            UpdateLoginButtonText();
            if (_fxcm.Status != ConnectStatus.Connected && loginAttempted)
            {
                MessageBox.Show("Unable to login", "Failed", MessageBoxButton.OK);
            }
        }

        private void UpdateLoginButtonText()
        {
            if (_fxcm.Status == ConnectStatus.Connected)
            {
                LoginOutButtonText = "Logout";
            }
            else
            {
                LoginOutButtonText = "Login";
            }
        }
    }
}
