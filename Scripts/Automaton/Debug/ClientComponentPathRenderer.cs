namespace CryoFall.Automaton.Debug
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using AtomicTorch.CBND.CoreMod.Systems.Notifications;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.ClientComponents;
    using AtomicTorch.CBND.GameApi.ServicesClient;
    using AtomicTorch.GameEngine.Common.Primitives;

    public class ClientComponentPathRenderer : ClientComponent
    {

        private static bool isDrawing;
        public static bool IsDrawing {
            get => isDrawing;
            set {
                Api.Logger.Dev($"Automaton: previous renderer {isDrawing}, current {value}");
                if (!isDrawing && value)
                {
                    Instance.OnEnable();
                }
                if (isDrawing && !value)
                {
                    Instance.OnDisable();
                }

                isDrawing = value;
            }
        }

        private static readonly IInputClientService Input = Client.Input;

        private static ClientComponentPathRenderer instance;
        public static ClientComponentPathRenderer Instance { 
            get
            {
                if (instance == null)
                {
                    instance = Client.Scene.CreateSceneObject("Automaton path lines renderer")
                        .AddComponent<ClientComponentPathRenderer>();
                }
                return instance;
            }
        }

        private Line[] lines = new Line[0];
        private List<Vector2D> points;

        public ClientComponentPathRenderer() : base(isLateUpdateEnabled: true)
        {
        }

        public override void LateUpdate(double deltaTime)
        {
            if (lines.Length == 0)
            {
                return;
            }

            // var mouseWorldPosition = Input.MouseWorldPosition;
            // var worldCellX = (int)mouseWorldPosition.X;
            // var worldCellY = (int)mouseWorldPosition.Y;
            // var screenPositionCurrent = Input.WorldToScreenPosition((worldCellX, worldCellY));
            // var screenPositionNext = Input.WorldToScreenPosition((worldCellX + 1, worldCellY + 1));
            // var screenWidth = Client.UI.ScreenWidth;
            // var screenHeight = Client.UI.ScreenHeight;

            // adjust with the reverse screen scale coefficient
            var scale = 1 / Api.Client.UI.GetScreenScaleCoefficient();

            for (var index = 0; index < lines.Length; index++)
            {
                var line = this.lines[index];
                
                var start = Input.WorldToScreenPosition(points[index]) * scale;
                var end = Input.WorldToScreenPosition(points[index + 1]) * scale;

                line.X1 = start.X;
                line.Y1 = start.Y;
                line.X2 = end.X;
                line.Y2 = end.Y;
            }
        }

        public void SetPoints(List<Vector2D> points)
        {
            if (!IsDrawing)
            {
                return;
            }

            OnDisable();
            this.points = points;
            NotificationSystem.ClientShowNotification("Setting points to\n[br]" + points.ConvertAll(p => "X: " + (int)p.X +  " Y: " + (int)p.Y).Aggregate((agg, p) => agg + "\n[br]" + p));
            OnEnable();
        }

        private void RemoveLines()
        {
            foreach (var line in this.lines)
            {
                Api.Client.UI.LayoutRootChildren.Remove(line);
            }

            this.lines = new Line[0];
        }

        protected override void OnDisable()
        {
            RemoveLines();

            NotificationSystem.ClientShowNotification("Automaton debug lines off");
        }

        protected override void OnEnable()
        {
            if (lines.Length != 0)
            {
                RemoveLines();
            }

            if (points == null || points.Count == 0)
            {
                Api.Logger.Error("Automaton: no points were passed to the renderer ");
                return;
            }

            var brush = new SolidColorBrush(Color.FromArgb(0xFF / 2, 0xFF, 0x00, 0x00));
            this.lines = new Line[points.Count - 1];
            for (var i = 0; i < lines.Length; i++)
            {
                var line = new Line
                {
                    StrokeThickness = 0.5f,
                    Stroke = brush
                };

                Api.Client.UI.LayoutRootChildren.Add(line);
                this.lines[i] = line;
            }

            NotificationSystem.ClientShowNotification("Automaton debug lines on");
        }
    }
}