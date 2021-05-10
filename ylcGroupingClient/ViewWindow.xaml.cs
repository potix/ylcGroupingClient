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
using System.Diagnostics;

namespace ylcGroupingClient
{
    /// <summary>
    /// ViewWindow.xaml の相互作用ロジック
    /// </summary>

    public partial class ViewWindow : Window
    {
        private volatile bool isClosed;
        private readonly YlccProtocol protocol = new YlccProtocol();
        private Collection<TextBox> _textBoxes = new Collection<TextBox>();

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
            Height = setting.MaxHeight + 20;
            int boxWidth = (setting.MaxWidth - ((setting.Choices.Count + 1) * setting.Padding)) / setting.Choices.Count;
            int LabelBoxHeight = setting.FontSize + setting.Padding;
            int ChoiceBoxHeight = (setting.FontSize * 3) + setting.Padding;
            int MessageBoxHeight = setting.MaxHeight - LabelBoxHeight - ChoiceBoxHeight - (setting.Padding * 2);
            int posX = setting.Padding;
            for (int idx = 0; idx < setting.Choices.Count; idx += 1)
            {
                _renderLabelBox(setting, boxWidth, LabelBoxHeight, posX, setting.Padding, idx);
                _renderChoiceBox(setting, boxWidth, ChoiceBoxHeight, posX, setting.Padding + LabelBoxHeight, idx);
                _textBoxes.Add(_renderMessageBox(setting, boxWidth, MessageBoxHeight, posX, setting.Padding + LabelBoxHeight + ChoiceBoxHeight, idx));
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
                        Debug.Print("Label " + response.GroupingActiveLiveChatMessage.Label);
                        Debug.Print("Choice " + response.GroupingActiveLiveChatMessage.Choice);
                        Debug.Print("GroupIdx " + response.GroupingActiveLiveChatMessage.GroupIdx);
                        Debug.Print("AuthorDisplayName " + response.GroupingActiveLiveChatMessage.ActiveLiveChatMessage.AuthorDisplayName);
                        Debug.Print("DisplayMessage " + response.GroupingActiveLiveChatMessage.ActiveLiveChatMessage.DisplayMessage);
                        // delete custom emoji
                        string noCustomDisplayMessage = System.Text.RegularExpressions.Regex.Replace(response.GroupingActiveLiveChatMessage.ActiveLiveChatMessage.DisplayMessage, ":[^:]+?:", "").Trim();
                        if (noCustomDisplayMessage == "")
                        {
                            continue;
                        } 　
                        StringBuilder sb = new StringBuilder();
                        sb.Append(noCustomDisplayMessage);
                        sb.Append(" (");
                        sb.Append(response.GroupingActiveLiveChatMessage.ActiveLiveChatMessage.AuthorDisplayName);
                        sb.Append(")");
                        sb.Append("\n");
                        _textBoxes[response.GroupingActiveLiveChatMessage.GroupIdx].Text = sb.ToString() + _textBoxes[response.GroupingActiveLiveChatMessage.GroupIdx].Text;
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

        private void _renderLabelBox(Setting setting, int width, int height, int posX, int posY, int idx)
        {
            TextBox textBox = new TextBox();
            textBox.Text = (idx + 1).ToString() + ".";
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

        private void _renderChoiceBox(Setting setting, int width, int height, int posX, int posY, int idx)
        {
            TextBox textBox = new TextBox();
            textBox.SetBinding(TextBox.TextProperty, "Text");
            textBox.DataContext = setting.Choices[idx];
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

        private TextBox _renderMessageBox(Setting setting, int width, int height, int posX, int posY, int idx)
        {
            TextBox textBox = new TextBox();
            textBox.Text = "";
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
            textBox.IsReadOnly = true;
            ViewGrid.Children.Add(textBox);
            return textBox;
        }
    }
}
