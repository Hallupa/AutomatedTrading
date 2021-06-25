using System;
using System.Collections.ObjectModel;
using Hallupa.Library;

namespace StrategyEditor.ViewModels
{
    public class CreatePointViewModel
    {
        private readonly Action _closeAction;

        public CreatePointViewModel(Action closeAction)
        {
            _closeAction = closeAction;
            Options.Add(MLPointType.Buy);
            Options.Add(MLPointType.Sell);
            Options.Add(MLPointType.Hold);
            OkCommand = new DelegateCommand(o =>
            {
                Ok = true;
                _closeAction();
            });
            CancelCommand = new DelegateCommand(o =>
            {
                _closeAction();
            });
        }

        public ObservableCollection<MLPointType> Options { get; private set; } = new ObservableCollection<MLPointType>();
        public MLPointType SelectedOption { get; set; } = MLPointType.Buy;
        public DelegateCommand OkCommand { get; private set; }
        public DelegateCommand CancelCommand { get; private set; }
        public bool Ok { get; private set; } = false;
    }
}