using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VK_UI3.DB;
using VK_UI3.Views;
using VK_UI3.VKs;
using VkNet.Model;

namespace VK_UI3.Views.LoginWindow
{
    public sealed partial class TokenLogin : Page
    {
        public TokenLogin()
        {
            this.InitializeComponent();
            this.Loaded += (_, _) =>
            {
                TokenTextBox.Focus(FocusState.Pointer);
                MainWindow.mainWindow.MainWindow_hideRefresh();
            };
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await TryLoginWithToken();
        }

        private async void TokenTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                await TryLoginWithToken();
        }

        private async Task TryLoginWithToken()
        {
            var token = TokenTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                ShowError("Введите токен");
                return;
            }

            errorTextBlock.Visibility = Visibility.Collapsed;
            Frame.Navigate(typeof(waitPage), null, new DrillInNavigationTransitionInfo());

            try
            {
                var vkAuth = new VKTokenAuth();
                await vkAuth.LoginWithTokenAsync(token, Frame);
            }
            catch (Exception ex)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    Frame.GoBack();
                    ShowError(ex.Message);
                });
            }
        }

        private void ShowError(string message)
        {
            errorTextBlock.Text = message;
            errorTextBlock.Visibility = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Login), null, new DrillInNavigationTransitionInfo());
        }
    }
}
