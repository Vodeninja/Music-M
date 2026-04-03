using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading;
using System.Threading.Tasks;
using VK_UI3.VKs;

namespace VK_UI3.Views.LoginWindow
{
    public sealed partial class QrLogin : Page
    {
        private CancellationTokenSource _cts;
        private VKQrAuth _qrAuth;

        public QrLogin()
        {
            this.InitializeComponent();
            _qrAuth = new VKQrAuth();
            this.Loaded += QrLogin_Loaded;
            this.Unloaded += QrLogin_Unloaded;
        }

        private async void QrLogin_Loaded(object sender, RoutedEventArgs e)
        {
            MainWindow.mainWindow.MainWindow_hideRefresh();
            await StartQrFlow();
        }

        private void QrLogin_Unloaded(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        private async Task StartQrFlow()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            errorTextBlock.Visibility = Visibility.Collapsed;
            RefreshButton.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = true;
            QrImage.Visibility = Visibility.Collapsed;
            StatusText.Text = "Генерация QR кода...";

            try
            {
                var qrImage = await _qrAuth.GenerateQrImageAsync(this.DispatcherQueue);

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    QrImage.Source = qrImage;
                    QrImage.Visibility = Visibility.Visible;
                    LoadingRing.IsActive = false;
                    StatusText.Text = "Ожидание сканирования...";
                });

                await _qrAuth.PollAsync(_cts.Token, OnStatusChanged, OnSuccess, OnError);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                OnError(ex.Message);
            }
        }

        private void OnStatusChanged(string status)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = status;
            });
        }

        private void OnSuccess()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                Frame.Navigate(typeof(Views.MainView), null, new DrillInNavigationTransitionInfo());
            });
        }

        private void OnError(string message)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                LoadingRing.IsActive = false;
                StatusText.Text = "Ошибка";
                errorTextBlock.Text = message;
                errorTextBlock.Visibility = Visibility.Visible;
                RefreshButton.Visibility = Visibility.Visible;
            });
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await StartQrFlow();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            Frame.Navigate(typeof(Login), null, new DrillInNavigationTransitionInfo());
        }
    }
}
