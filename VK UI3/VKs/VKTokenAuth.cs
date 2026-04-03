using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using VK_UI3.DB;
using VK_UI3.Views;
using VkNet.Abstractions;
using VkNet.Enums.Filters;
using VkNet.Model;

namespace VK_UI3.VKs
{
    /// <summary>
    /// Авторизация по access_token напрямую
    /// </summary>
    internal class VKTokenAuth : VK
    {
        private readonly IVkApi _api = App._host.Services.GetRequiredService<IVkApi>();

        public async Task LoginWithTokenAsync(string token, Frame frame)
        {
            await _api.AuthorizeAsync(new ApiAuthParams
            {
                AccessToken = token
            });

            var profiles = await _api.Users.GetAsync(new List<long>(), ProfileFields.PhotoMax);
            var profile = profiles.Count > 0 ? profiles[0] : null;

            if (profile == null)
                throw new Exception("Не удалось получить данные профиля. Проверьте токен.");

            AccountsDB.activeAccount.Token = token;
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
            AccountsDB.activeAccount.Update();

            frame.DispatcherQueue.TryEnqueue(() =>
            {
                frame.Navigate(typeof(MainView), null, new DrillInNavigationTransitionInfo());
            });
        }
    }
}
