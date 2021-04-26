﻿using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using OpenTabletDriver.Desktop;

namespace OpenTabletDriver.UX.Controls.Area
{
    public class AreaViewModel : ViewModel
    {
        private float w, h, x, y, r;
        private string unit, invalidForegroundError, invalidBackgroundError;
        private bool lockArea, enableRotation;
        private IEnumerable<RectangleF> bg;
        private RectangleF fullbg;

        public float Width
        {
            set => this.RaiseAndSetIfChanged(ref this.w, value);
            get => this.w;
        }

        public float Height
        {
            set => this.RaiseAndSetIfChanged(ref this.h, value);
            get => this.h;
        }

        public float X
        {
            set => this.RaiseAndSetIfChanged(ref this.x, value);
            get => this.x;
        }

        public float Y
        {
            set => this.RaiseAndSetIfChanged(ref this.y, value);
            get => this.y;
        }

        public float Rotation
        {
            set => this.RaiseAndSetIfChanged(ref this.r, value);
            get => this.r;
        }

        public bool LockToUsableArea
        {
            set => this.RaiseAndSetIfChanged(ref this.lockArea, value);
            get => this.lockArea;
        }

        public bool EnableRotation
        {
            set => this.RaiseAndSetIfChanged(ref this.enableRotation, value);
            get => this.enableRotation;
        }

        public IEnumerable<RectangleF> Background
        {
            set
            {
                this.RaiseAndSetIfChanged(ref this.bg, value);
                if (Background != null)
                {
                    this.FullBackground = new RectangleF
                    {
                        Left = this.Background.Min(r => r.Left),
                        Top = this.Background.Min(r => r.Top),
                        Right = this.Background.Max(r => r.Right),
                        Bottom = this.Background.Max(r => r.Bottom),
                    };
                }
                else
                {
                    this.FullBackground = RectangleF.Empty;
                }
            }
            get => this.bg;
        }

        public RectangleF FullBackground
        {
            private set => this.RaiseAndSetIfChanged(ref this.fullbg, value);
            get => this.fullbg;
        }

        public string Unit
        {
            set => this.RaiseAndSetIfChanged(ref this.unit, value);
            get => this.unit;
        }

        public string InvalidForegroundError
        {
            set => this.RaiseAndSetIfChanged(ref this.invalidForegroundError, value);
            get => this.invalidForegroundError ??= "Invalid area size.";
        }

        public string InvalidBackgroundError
        {
            set => this.RaiseAndSetIfChanged(ref this.invalidBackgroundError, value);
            get => this.invalidBackgroundError ??= "Invalid background size.";
        }
    }
}
