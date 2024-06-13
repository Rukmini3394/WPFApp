using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Newtonsoft.Json.Linq;
using System.Text;

namespace active_directory_b2c_wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string[] ApiScopes = new string[] { "https://infrab2c.onmicrosoft.com/c41e4bda-9340-4d05-8f3e-079bd5f36043/helloapi/demo.read" };

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            var app = App.PublicClientApp;
            AuthenticationResult authResult = null;
            try
            {
                ResultText.Text = "";
                authResult = await app.AcquireTokenInteractive(ApiScopes)
                    .WithParentActivityOrWindow(new WindowInteropHelper(this).Handle)
                    .ExecuteAsync();

                if (authResult != null && !string.IsNullOrEmpty(authResult.AccessToken))
                {
                    DisplayUserInfo(authResult);
                    UpdateSignInState(true);
                }
                else
                {
                    ResultText.Text = "Failed to acquire token.";
                }
            }
            catch (MsalException ex)
            {
                HandleMsalException(ex, app);
            }
            catch (Exception ex)
            {
                ResultText.Text = $"Error Acquiring Token: {ex.Message}";
            }
        }

        private async void EditProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var app = App.PublicClientApp;
            try
            {
                ResultText.Text = $"Calling API: {App.AuthorityEditProfile}";

                AuthenticationResult authResult = await app.AcquireTokenInteractive(ApiScopes)
                    .WithParentActivityOrWindow(new WindowInteropHelper(this).Handle)
                    .WithB2CAuthority(App.AuthorityEditProfile)
                    .WithPrompt(Prompt.NoPrompt)
                    .ExecuteAsync();

                if (authResult != null && !string.IsNullOrEmpty(authResult.AccessToken))
                {
                    DisplayUserInfo(authResult);
                }
                else
                {
                    ResultText.Text = "Failed to acquire token.";
                }
            }
            catch (Exception ex)
            {
                ResultText.Text = $"Session has expired, please sign out and back in. {ex.Message}";
            }
        }

        private async void CallApiButton_Click(object sender, RoutedEventArgs e)
        {
            var app = App.PublicClientApp;
            var accounts = await app.GetAccountsAsync(App.PolicySignUpSignIn);
            AuthenticationResult authResult = null;

            try
            {
                authResult = await app.AcquireTokenSilent(ApiScopes, accounts.FirstOrDefault())
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                try
                {
                    authResult = await app.AcquireTokenInteractive(ApiScopes)
                        .WithParentActivityOrWindow(new WindowInteropHelper(this).Handle)
                        .ExecuteAsync();
                }
                catch (MsalException ex)
                {
                    ResultText.Text = $"Error Acquiring Token: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                ResultText.Text = $"Error Acquiring Token Silently: {ex.Message}";
                return;
            }

            if (authResult != null && !string.IsNullOrEmpty(authResult.AccessToken))
            {
                ResultText.Text = await GetHttpContentWithToken(App.ApiEndpoint, authResult.AccessToken);
                DisplayUserInfo(authResult);
            }
            else
            {
                ResultText.Text = "Access token is null (could be expired). Please do interactive log-in again.";
            }
        }

        private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            var app = App.PublicClientApp;
            var accounts = await app.GetAccountsAsync();
            try
            {
                while (accounts.Any())
                {
                    await app.RemoveAsync(accounts.FirstOrDefault());
                    accounts = await app.GetAccountsAsync();
                }

                UpdateSignInState(false);
            }
            catch (MsalException ex)
            {
                ResultText.Text = $"Error signing-out user: {ex.Message}";
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var app = App.PublicClientApp;
            var accounts = await app.GetAccountsAsync(App.PolicySignUpSignIn);
            try
            {
                AuthenticationResult authResult = await app.AcquireTokenSilent(ApiScopes, accounts.FirstOrDefault())
                    .ExecuteAsync();

                if (authResult != null && !string.IsNullOrEmpty(authResult.AccessToken))
                {
                    DisplayUserInfo(authResult);
                    UpdateSignInState(true);
                }
                else
                {
                    ResultText.Text = "You need to sign-in first, and then Call API";
                }
            }
            catch (MsalUiRequiredException)
            {
                ResultText.Text = "You need to sign-in first, and then Call API";
            }
            catch (Exception ex)
            {
                ResultText.Text = $"Error Acquiring Token Silently: {ex.Message}";
            }
        }

        private void UpdateSignInState(bool signedIn)
        {
            SignInButton.Visibility = signedIn ? Visibility.Collapsed : Visibility.Visible;
            CallApiButton.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
            EditProfileButton.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
            SignOutButton.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
            if (!signedIn)
            {
                ResultText.Text = "";
                TokenInfoText.Text = "";
            }
        }

        private void DisplayUserInfo(AuthenticationResult authResult)
        {
            if (authResult != null)
            {
                JObject user = ParseIdToken(authResult.IdToken);

                TokenInfoText.Text = "";
                TokenInfoText.Text += $"Name: {user["name"]?.ToString()}" + Environment.NewLine;
                TokenInfoText.Text += $"User Identifier: {user["oid"]?.ToString()}" + Environment.NewLine;
                TokenInfoText.Text += $"Street Address: {user["streetAddress"]?.ToString()}" + Environment.NewLine;
                TokenInfoText.Text += $"City: {user["city"]?.ToString()}" + Environment.NewLine;
                TokenInfoText.Text += $"State: {user["state"]?.ToString()}" + Environment.NewLine;
                TokenInfoText.Text += $"Country: {user["country"]?.ToString()}" + Environment.NewLine;
                TokenInfoText.Text += $"Job Title: {user["jobTitle"]?.ToString()}" + Environment.NewLine;

                if (user["emails"] is JArray emails)
                {
                    TokenInfoText.Text += $"Emails: {emails[0].ToString()}" + Environment.NewLine;
                }
                TokenInfoText.Text += $"Identity Provider: {user["iss"]?.ToString()}" + Environment.NewLine;
            }
        }

        private JObject ParseIdToken(string idToken)
        {
            idToken = idToken.Split('.')[1];
            idToken = Base64UrlDecode(idToken);
            return JObject.Parse(idToken);
        }

        private string Base64UrlDecode(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
            var byteArray = Convert.FromBase64String(s);
            return Encoding.UTF8.GetString(byteArray, 0, byteArray.Count());
        }

        private async Task<string> GetHttpContentWithToken(string url, string token)
        {
            var httpClient = new HttpClient();
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }

        private async void HandleMsalException(MsalException ex, IPublicClientApplication app)
        {
            try
            {
                if (ex.Message.Contains("AADB2C90118"))
                {
                    var authResult = await app.AcquireTokenInteractive(ApiScopes)
                        .WithParentActivityOrWindow(new WindowInteropHelper(this).Handle)
                        .WithPrompt(Prompt.SelectAccount)
                        .WithB2CAuthority(App.AuthorityResetPassword)
                        .ExecuteAsync();

                    if (authResult != null && !string.IsNullOrEmpty(authResult.AccessToken))
                    {
                        DisplayUserInfo(authResult);
                        UpdateSignInState(true);
                    }
                    else
                    {
                        ResultText.Text = "Failed to acquire token.";
                    }
                }
                else
                {
                    ResultText.Text = $"Error Acquiring Token: {ex.Message}";
                }
            }
            catch (Exception exe)
            {
                ResultText.Text = $"Error Acquiring Token: {exe.Message}";
            }
        }
    }
}
