using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VK_UI3.DB;
using VK_UI3.Views;
using VkNet.Abstractions;
using VkNet.AudioBypassService.Models.Auth;
using VkNet.Enums.Filters;
using IAuthCategory = VkNet.AudioBypassService.Abstractions.Categories.IAuthCategory;
using VkNet.Model;
using Windows.Storage.Streams;
using System.Collections.Generic;

namespace VK_UI3.VKs
{
    /// <summary>
    /// Авторизация через QR код (VK Auth Code)
    /// </summary>
    internal class VKQrAuth : VK
    {
        private readonly IAuthCategory _authCategory = App._host.Services.GetRequiredService<IAuthCategory>();
        private readonly IVkApi _api = App._host.Services.GetRequiredService<IVkApi>();

        private AuthCodeResponse _authCode;

        /// <summary>
        /// Генерирует QR код и возвращает BitmapImage для отображения
        /// </summary>
        public async Task<BitmapImage> GenerateQrImageAsync(DispatcherQueue dispatcherQueue)
        {
            _authCode = await _authCategory.GetAuthCodeAsync("VK M Player", forceRegenerate: true);

            using var generator = new QRCodeGenerator();
            using var qrData = generator.CreateQrCode(_authCode.AuthUrl, QRCodeGenerator.ECCLevel.Q);
            var pngQr = new PngByteQRCode(qrData);
            var pngBytes = pngQr.GetGraphic(10);

            var ras = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(ras.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(pngBytes);
                await writer.StoreAsync();
            }

            var tcs = new TaskCompletionSource<BitmapImage>();
            dispatcherQueue.TryEnqueue(async () =>
            {
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(ras);
                tcs.SetResult(bmp);
            });

            return await tcs.Task;
        }

        /// <summary>
        /// Поллинг статуса QR кода до успешной авторизации или ошибки
        /// </summary>
        public async Task PollAsync(
            CancellationToken ct,
            Action<string> onStatusChanged,
            Action onSuccess,
            Action<string> onError)
        {
            if (_authCode == null)
            {
                onError("QR код не был сгенерирован");
                return;
            }

            var deadline = DateTime.UtcNow.AddSeconds(_authCode.ExpiresIn > 0 ? _authCode.ExpiresIn : 180);

            while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                try
                {
                    await Task.Delay(3000, ct);

                    var check = await _authCategory.CheckAuthCodeAsync(_authCode.AuthHash, _authCode.Token);

                    switch (check.Status)
                    {
                        case Statuses.Continue:
                            onStatusChanged("Ожидание сканирования...");
                            break;

                        case Statuses.ConfirmOnPhone:
                            onStatusChanged("Подтвердите вход в приложении ВКонтакте...");
                            break;

                        case Statuses.Ok:
                            await FinalizeLoginAsync(check);
                            onSuccess();
                            return;

                        case Statuses.Expired:
                            onError("QR код истёк. Нажмите «Обновить QR код».");
                            return;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    onError(ex.Message);
                    return;
                }
            }

            if (!ct.IsCancellationRequested)
                onError("QR код истёк. Нажмите «Обновить QR код».");
        }

        private async Task FinalizeLoginAsync(AuthCheckResponse check)
        {
            if (!string.IsNullOrEmpty(check.super_app_token))
            {
                await _api.AuthorizeAsync(new ApiAuthParams
                {
                    AccessToken = check.super_app_token
                });

                AccountsDB.activeAccount.Token = check.super_app_token;
            }

            var profiles = await _api.Users.GetAsync(new List<long>(), ProfileFields.PhotoMax);
            var profile = profiles.Count > 0 ? profiles[0] : null;

            if (profile != null)
            {
                AccountsDB.activeAccount.id = profile.Id;
                AccountsDB.activeAccount.Name = $"{profile.FirstName} {profile.LastName}";
                AccountsDB.activeAccount.UserPhoto = (
                    profile.PhotoMax ??
                    profile.Photo400Orig ??
                    profile.Photo200Orig ??
                    profile.Photo200 ??
                    profile.Photo100 ??
                    new Uri("https://vk.ru/images/camera_200.png")
                ).ToString();
            }

            AccountsDB.activeAccount.Update();
        }
    }
}
