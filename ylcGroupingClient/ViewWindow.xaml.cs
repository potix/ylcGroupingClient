using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ylccProtocol;
using Grpc.Net.Client;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Grpc.Core;

namespace ylcGroupingClient
{
    /// <summary>
    /// ViewWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ViewWindow : Window
    {
        private volatile bool isClosed;
        private readonly YlccProtocol protocol = new YlccProtocol();

        public ViewWindow()
        {
            InitializeComponent();
            isClosed = false;
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            isClosed = true;
        }

        public async void StartGrouping(Setting setting)
        {
            System.Drawing.Color dColor = System.Drawing.ColorTranslator.FromHtml(setting.WindowBackgroundColor);
            System.Windows.Media.Color mColor = System.Windows.Media.Color.FromArgb(dColor.A, dColor.R, dColor.G, dColor.B);
            Background = new SolidColorBrush(mColor);
            Width = setting.MaxWidth + 10;
            Height = setting.MaxHeight + 40;
            int boxWidth = (setting.MaxWidth - ((setting.Choices.Count + 1) * setting.Padding)) / setting.Choices.Count;
            int ChoiceBoxHeight = (setting.FontSize * 4) + (setting.Padding * 2);
            int MessageBoxHeight = setting.MaxHeight - ChoiceBoxHeight - (setting.Padding * 3);
            int posX = setting.Padding;
            foreach (Choice choice in setting.Choices)
            {
                _renderChoicesBox(setting, boxWidth, ChoiceBoxHeight, posX, setting.Padding, choice);
                _renderMessageBox(setting, boxWidth, MessageBoxHeight, posX, setting.Padding + ChoiceBoxHeight, choice);
                posX += (boxWidth + setting.Padding);
            }
            try
            {
                if (setting.IsInsecure)
                {
                    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                }
                Collection<GroupingChoice> choices = new Collection<GroupingChoice>();
                foreach (var choiceItem in setting.Choices)
                {
                    int idx = setting.Choices.IndexOf(choiceItem);
                    choices.Add(protocol.BuildGroupingCoice((idx + 1).ToString(), choiceItem.Text));
                }
                GrpcChannel channel = GrpcChannel.ForAddress(setting.Uri);
                ylcc.ylccClient client = new ylcc.ylccClient(channel);
                StartGroupingActiveLiveChatRequest startGroupingActiveLiveChatRequest = protocol.BuildStartGroupingActiveLiveChatRequest(setting.VideoId, setting.TargetValue.Target, choices);
                StartGroupingActiveLiveChatResponse startGroupingActiveLiveChatResponse = await client.StartGroupingActiveLiveChatAsync(startGroupingActiveLiveChatRequest);
                if (startGroupingActiveLiveChatResponse.Status.Code != Code.Success && startGroupingActiveLiveChatResponse.Status.Code != Code.InProgress)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("通信エラー\n");
                    sb.Append("URI:" + setting.Uri + "\n");
                    sb.Append("VideoId:" + setting.VideoId + "\n");
                    sb.Append("Reason:" + startGroupingActiveLiveChatResponse.Status.Message + "\n");
                    MessageBox.Show(sb.ToString());
                    this.Close();
                    return;
                }
                PollGroupingActiveLiveChatRequest pollGroupingActiveLiveChatRequest = protocol.BuildPollGroupingActiveLiveChatRequest(startGroupingActiveLiveChatResponse.GroupingId);
                AsyncServerStreamingCall<PollGroupingActiveLiveChatResponse> call = client.PollGroupingActiveLiveChat(pollGroupingActiveLiveChatRequest);
                while (!isClosed && await call.ResponseStream.MoveNext())
                {
                    PollGroupingActiveLiveChatResponse response = call.ResponseStream.Current;
                    if (response.Status.Code != Code.Success)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append("通信エラー\n");
                        sb.Append("URI:" + setting.Uri + "\n");
                        sb.Append("VideoId:" + setting.VideoId + "\n");
                        sb.Append("Reason:" + response.Status.Message + "\n");
                        MessageBox.Show(sb.ToString());
                        this.Close();
                        break;
                    }
                    if (response.GroupingActiveLiveChatMessage != null)
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        stringBuilder.Append("[");
                        stringBuilder.Append(response.GroupingActiveLiveChatMessage.ActiveLiveChatMessage.AuthorDisplayName);
                        stringBuilder.Append("]:");
                        stringBuilder.Append(response.GroupingActiveLiveChatMessage.ActiveLiveChatMessage.DisplayMessage);
                        stringBuilder.Append("\n");
                        setting.Choices[response.GroupingActiveLiveChatMessage.GroupIdx].Result = stringBuilder.ToString() + setting.Choices[response.GroupingActiveLiveChatMessage.GroupIdx].Result;
                    }
                }
                await channel.ShutdownAsync();
            }
            catch (Exception e)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("通信エラー\n");
                sb.Append("URI:" + setting.Uri + "\n");
                sb.Append("VideoId:" + setting.VideoId + "\n");
                sb.Append("Reason:" + e.Message + "\n");
                MessageBox.Show(sb.ToString());
                this.Close();
            }
        }

        private void _renderChoicesBox(Setting setting, int width, int height, int posX, int posY, Choice choice)
        {
            TextBox textBox = new TextBox();
            textBox.SetBinding(TextBox.TextProperty, "Text");
            textBox.DataContext = choice;
            textBox.FontSize = setting.FontSize;
            textBox.BorderThickness = new Thickness(0);
            textBox.HorizontalAlignment = HorizontalAlignment.Left;
            textBox.VerticalAlignment = VerticalAlignment.Top;
            textBox.Margin = new Thickness(posX, posY, 0, 0);
            System.Drawing.Color dColor = System.Drawing.ColorTranslator.FromHtml(setting.BoxBackgroundColor);
            System.Windows.Media.Color mColor = System.Windows.Media.Color.FromArgb(dColor.A, dColor.R, dColor.G, dColor.B);
            textBox.Background = new SolidColorBrush(mColor);
            dColor = System.Drawing.ColorTranslator.FromHtml(setting.BoxForegroundColor);
            mColor = System.Windows.Media.Color.FromArgb(dColor.A, dColor.R, dColor.G, dColor.B);
            textBox.Foreground = new SolidColorBrush(mColor);
            textBox.HorizontalContentAlignment = HorizontalAlignment.Center;
            textBox.VerticalContentAlignment = VerticalAlignment.Top;
            textBox.Width = width;
            textBox.Height = height;
            textBox.TextWrapping = TextWrapping.Wrap;
            ViewGrid.Children.Add(textBox);
        }

        private void _renderMessageBox(Setting setting, int width, int height, int posX, int posY, Choice choice)
        {
            TextBox textBox = new TextBox();
            textBox.SetBinding(TextBox.TextProperty, "Result");
            textBox.DataContext = choice;
            textBox.FontSize = setting.FontSize;
            textBox.BorderThickness = new Thickness(0);
            textBox.HorizontalAlignment = HorizontalAlignment.Left;
            textBox.VerticalAlignment = VerticalAlignment.Top;
            textBox.Margin = new Thickness(posX, posY, 0, 0);
            System.Drawing.Color dColor = System.Drawing.ColorTranslator.FromHtml(setting.BoxBackgroundColor);
            System.Windows.Media.Color mColor = System.Windows.Media.Color.FromArgb(dColor.A, dColor.R, dColor.G, dColor.B);
            textBox.Background = new SolidColorBrush(mColor);
            dColor = System.Drawing.ColorTranslator.FromHtml(setting.BoxForegroundColor);
            mColor = System.Windows.Media.Color.FromArgb(dColor.A, dColor.R, dColor.G, dColor.B);
            textBox.Foreground = new SolidColorBrush(mColor);
            textBox.HorizontalContentAlignment = HorizontalAlignment.Left;
            textBox.VerticalContentAlignment = VerticalAlignment.Top;
            textBox.Width = width;
            textBox.Height = height;
            textBox.TextWrapping = TextWrapping.Wrap;
            ViewGrid.Children.Add(textBox);
        }
    }
}
