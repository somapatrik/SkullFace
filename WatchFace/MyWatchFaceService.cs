using System;
using System.Threading;
using Android.App;
using Android.Util;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Text.Format;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Support.Wearable.Watchface;
using Android.Service.Wallpaper;
using Java.Util.Concurrent;

namespace WatchFace
{
    // MyWatchFaceService implements only one method, OnCreateEngine, 
    // and it defines a nested class that is derived from
    // CanvasWatchFaceService.Engine.

    public class MyWatchFaceService : CanvasWatchFaceService
    {
        // Used for logging:
        const String Tag = "MyWatchFaceService";

        // Must be implemented to return a new instance of the wallpaper's engine:
        public override WallpaperService.Engine OnCreateEngine () 
        {
            return new MyWatchFaceEngine (this);
        }

        public class MyWatchFaceEngine : CanvasWatchFaceService.Engine 
        {
            // Update every second:
            static long InterActiveUpdateRateMs = TimeUnit.Seconds.ToMillis (1);

            // Reference to the CanvasWatchFaceService that instantiates this engine:
            CanvasWatchFaceService owner;

            // For painting the hands of the watch:
            Paint timePaint;

            // For painting the tick marks around the edge of the clock face:
            Paint tickPaint;

            // The current time:
            public Time time;
            //Timer timerSeconds;

            // Broadcast receiver for handling time zone changes:
            TimeZoneReceiver timeZoneReceiver;

            // Whether the display supports fewer bits for each color in ambient mode. 
            // When true, we disable anti-aliasing in ambient mode:
            bool lowBitAmbient;

            // Bitmaps for drawing the watch face background:
            Bitmap backgroundImage; 
            Bitmap backgroundScaledBitmap;

            // Saves a reference to the outer CanvasWatchFaceService
            public MyWatchFaceEngine (CanvasWatchFaceService owner) : base(owner)
            {
                this.owner = owner;
            }
 
            public override void OnCreate (ISurfaceHolder holder) 
            {
                base.OnCreate(holder);

                // Configure the system UI. Instantiates a WatchFaceStyle object that causes 
                // notifications to appear as small peek cards that are shown only briefly 
                // when interruptive. Also disables the system-style UI time from being drawn:

                SetWatchFaceStyle (new WatchFaceStyle.Builder (owner)
                    .SetCardPeekMode (WatchFaceStyle.PeekModeShort)
                    .SetBackgroundVisibility (WatchFaceStyle.BackgroundVisibilityInterruptive)
                    .SetShowSystemUiTime (false)
                    .Build ());

                // Configure the background image
                var backgroundDrawable = 
                    Application.Context.Resources.GetDrawable (Resource.Drawable.skull_background);
                backgroundImage = (backgroundDrawable as BitmapDrawable).Bitmap;

                // Time text
                timePaint = new Paint();
                timePaint.SetARGB(255, 0,0,0);
                timePaint.StrokeWidth = 2.0f;
                timePaint.AntiAlias = true;
                timePaint.TextSize = 50;
                timePaint.TextAlign = Paint.Align.Center;

                // Instantiate the time object:
                time = new Time();

                // Second timer example:
                //timerSeconds = new Timer (new TimerCallback (state => {
                //    Invalidate ();
                //}), null, 
                //    TimeSpan.FromMilliseconds (InterActiveUpdateRateMs), 
                //    TimeSpan.FromMilliseconds (InterActiveUpdateRateMs));
            }

            // Called when the properties of the Wear device are determined, specifically 
            // low bit ambient mode (the screen supports fewer bits for each color in
            // ambient mode):

            public override void OnPropertiesChanged(Bundle properties) 
            {
                base.OnPropertiesChanged (properties);

                lowBitAmbient = properties.GetBoolean (MyWatchFaceService.PropertyLowBitAmbient);

                if (Log.IsLoggable (Tag, LogPriority.Debug))
                    Log.Debug (Tag, "OnPropertiesChanged: low-bit ambient = " + lowBitAmbient);
            }

            // Called periodically to update the time shown by the watch face: at least 
            // once per minute in ambient and interactive modes, and whenever the date, 
            // time, or timezone has changed:

            public override void OnTimeTick ()
            {
                base.OnTimeTick ();

                if (Log.IsLoggable (Tag, LogPriority.Debug))
                    Log.Debug (Tag, "onTimeTick: ambient = " + IsInAmbientMode);
                
                Invalidate ();
            }

            // Called when the device enters or exits ambient mode. In ambient mode,
            // the watch face disables anti-aliasing while drawing.

            public override void OnAmbientModeChanged (bool inAmbientMode) 
            {
                base.OnAmbientModeChanged (inAmbientMode);

                if (Log.IsLoggable (Tag, LogPriority.Debug))
                    Log.Debug (Tag, "OnAmbientMode");
                
                if (lowBitAmbient)
                {
                   bool antiAlias = !inAmbientMode;
                    timePaint.AntiAlias = antiAlias;
                }

                Invalidate ();
            }

            // Called to draw the watch face:

            public override void OnDraw (Canvas canvas, Rect bounds)
            {
                // Get the current time:
                time.SetToNow();

                // Text with current time
                string timetext = time.Format("%H : %M");//  time.Hour.ToString() + " : " + time.Minute.ToString();

                // Determine the bounds of the drawing surface:
                int width = bounds.Width ();
                int height = bounds.Height ();

                // Draw the background, scaled to fit:
                if (backgroundScaledBitmap == null
                    || backgroundScaledBitmap.Width != width
                    || backgroundScaledBitmap.Height != height)
                {
                    backgroundScaledBitmap = Bitmap.CreateScaledBitmap(backgroundImage, width, height, true /* filter */);
                }
                canvas.DrawColor (Color.Black);
                canvas.DrawBitmap(backgroundScaledBitmap, 0, 0, null);

                float centerX = width / 2.0f;
                float centerY = height / 2.0f;

                float higherY = centerY - 50;

                canvas.DrawText(timetext, centerX, higherY, timePaint);
            }

            // Called whenever the watch face is becoming visible or hidden. Note that
            // you must call base.OnVisibilityChanged first:

            public override void OnVisibilityChanged (bool visible)
            {
                base.OnVisibilityChanged (visible);

                if (Log.IsLoggable (Tag, LogPriority.Debug))
                    Log.Debug (Tag, "OnVisibilityChanged: " + visible);
                
                // If the watch face became visible, register the timezone receiver
                // and get the current time. Else, unregister the timezone receiver:

                if (visible)
                {
                    RegisterTimezoneReceiver ();
                    time.Clear(Java.Util.TimeZone.Default.ID);
                    time.SetToNow();
                }
                else
                    UnregisterTimezoneReceiver ();
            }

            // Run the timer only when visible and in interactive mode:
            bool ShouldTimerBeRunning() 
            {
                return IsVisible && !IsInAmbientMode;
            }

            bool registeredTimezoneReceiver = false;

            // Registers the time zone broadcast receiver (defined at the end of 
            // this file) to handle time zone change events:

            void RegisterTimezoneReceiver()
            {
                if (registeredTimezoneReceiver)
                    return;
                else
                {
                    if (timeZoneReceiver == null)
                    {
                        timeZoneReceiver = new TimeZoneReceiver ();
                        timeZoneReceiver.Receive = (intent) => {
                            time.Clear (intent.GetStringExtra ("time-zone"));
                            time.SetToNow ();
                        };
                    }
                    registeredTimezoneReceiver = true;
                    IntentFilter filter = new IntentFilter(Intent.ActionTimezoneChanged);
                    Application.Context.RegisterReceiver (timeZoneReceiver, filter);
                }
            }

            // Unregisters the timezone Broadcast receiver:

            void UnregisterTimezoneReceiver() 
            {
                if (!registeredTimezoneReceiver)
                    return;
                registeredTimezoneReceiver = false;
                Application.Context.UnregisterReceiver (timeZoneReceiver);
            }
        }
    }

    // Time zone broadcast receiver. OnReceive is called when the
    // time zone changes:

    public class TimeZoneReceiver: BroadcastReceiver 
    {
        public Action<Intent> Receive { get; set; }

        public override void OnReceive (Context context, Intent intent)
        {
            if (Receive != null)
                Receive (intent);
        }
    }
}
