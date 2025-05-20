using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Services;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using Wpf.Ui;

namespace neo_bpsys_wpf.ViewModels
{
    public partial class GameGuidanceViewModel : ObservableObject
    {
        public ISharedDataService SharedDataService { get; }
        private readonly IMessageBoxService _messageBoxService;
        public GameGuidanceViewModel(ISharedDataService sharedDataService, IMessageBoxService messageBoxService)
        {
            SharedDataService = sharedDataService;
            _messageBoxService = messageBoxService;
        }

    }
}