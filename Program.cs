using System;
using System.Windows.Forms;
using Websocket.Client;
using DiscordRPC;
using MusicCat.Rpc.Models;
using MusicCat.Rpc.Services;

namespace MusicCat.Rpc;

class Program
{
    public const string DISCORD_APP_ID = "1275296537991319553";
    public const string MUSICCAT_WEBSOCKET_URL = "wss://bachtran.dev:7443/tracker-websocket";

    private static readonly Uri WsUrl = new(MUSICCAT_WEBSOCKET_URL);
    private static DiscordRpcClient? _discordClient;
    private static readonly StatusBuffer _buffer = new();
    private static MusicStatus? _previousStatus;
    private static NotifyIcon? _trayIcon;

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Setup system tray icon
        _trayIcon = new NotifyIcon()
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "MusicCat RPC - Starting..."
        };

        var contextMenu = new ContextMenuStrip();
        var exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => 
        {
            Application.Exit();
        });
        contextMenu.Items.Add(exitMenuItem);
        _trayIcon.ContextMenuStrip = contextMenu;

        // 1. Setup Discord
        _discordClient = new DiscordRpcClient(DISCORD_APP_ID);
        _discordClient.Initialize();
        UpdateTrayIcon("Connected to Discord");

        // 2. Setup WebSocket (run async setup on background thread)
        WebsocketClient? wsClient = null;
        Task.Run(async () =>
        {
            try
            {
                wsClient = new WebsocketClient(WsUrl)
                {
                    ReconnectTimeout = TimeSpan.FromSeconds(30)
                };

                wsClient.MessageReceived.Subscribe(msg =>
                {
                    if (string.IsNullOrEmpty(msg.Text)) return;
                    
                    var status = System.Text.Json.JsonSerializer.Deserialize<MusicStatus>(msg.Text);
                    if (status != null)
                    {
                        _buffer.Update(status);
                        
                        // Check if track info changed
                        if (HasTrackStatusChanged(_previousStatus, status))
                        {
                            UpdatePresence();
                            UpdateTrayIcon(status);
                            _previousStatus = status;
                        }
                    }
                });

                await wsClient.Start();
                UpdateTrayIcon("WebSocket connected");

                // Start Update Loop (for timestamp updates every 30 seconds)
                await _buffer.WaitForFirstData;
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    UpdatePresence();
                }
            }
            catch (Exception ex)
            {
                UpdateTrayIcon($"Error: {ex.Message}");
            }
        });

        // 3. Handle cleanup on exit
        Application.ApplicationExit += (s, e) =>
        {
            _trayIcon?.Dispose();
            _discordClient?.Dispose();
            wsClient?.Dispose();
        };

        Application.Run();
    }

    static bool HasTrackStatusChanged(MusicStatus? previous, MusicStatus? current)
    {
        if (previous == null || current == null) return true;
        if (previous.IsPlaying != current.IsPlaying) return true;
        if (previous.IsPaused != current.IsPaused) return true;
        
        var prevTrack = previous.Track;
        var currTrack = current.Track;
        
        return prevTrack.Uri != currTrack.Uri;
    }

    static void UpdatePresence()
    {
        var (status, lastUpdate, hasData) = _buffer.Get();
        if (!hasData || status == null || _discordClient == null) return;

        if (!status.IsPlaying || status.IsPaused)
        {
            _discordClient.ClearPresence();
            return;
        }

        // Calculate time logic
        var elapsed = DateTime.Now - lastUpdate;
        var start = DateTime.UtcNow.AddMilliseconds(-(status.Track.Position + elapsed.TotalMilliseconds));
        var end = start.AddMilliseconds(status.Track.Length);

        _discordClient.SetPresence(new RichPresence()
        {
            Type = ActivityType.Listening,
            StatusDisplay = StatusDisplayType.Details,
            Details = status.Track.Title,
            DetailsUrl = status.Track.Uri,
            State = status.Track.Author,
            Timestamps = status.Track.IsStream ? null : new Timestamps(start, end),
            Assets = new Assets()
            {
                LargeImageKey = status.Track.ArtworkUrl,
                SmallImageKey = status.Track.SourceName,
            }
        });
    }

    static void UpdateTrayIcon(string discordStatus)
    {
        if (_trayIcon == null) return;

        var tooltip = $"Discord: {discordStatus}\nTrack: Not playing";
        _trayIcon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 60) + "..." : tooltip;
    }

    static void UpdateTrayIcon(MusicStatus status)
    {
        if (_trayIcon == null) return;

        var discordStatus = _discordClient?.IsInitialized == true ? "Connected" : "Disconnected";
        string trackInfo;

        if (!status.IsPlaying)
        {
            trackInfo = "Not playing";
        }
        else
        {
            var trackDisplay = $"{status.Track.Title} - {status.Track.Author}";
            trackInfo = $"▶ {trackDisplay}";
        }

        var tooltip = $"Discord: {discordStatus}\nTrack: {trackInfo}";
        _trayIcon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 60) + "..." : tooltip;
    }
}