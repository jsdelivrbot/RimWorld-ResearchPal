// ResearchTree/LogHeadDB.cs
//
// Copyright Karel Kroeze, 2015.
//
// Created 2015-12-21 13:30

using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static ResearchPal.Constants;

namespace ResearchPal
{
    public class MainTabWindow_ResearchTree : MainTabWindow
    {
        internal static Vector2 _scrollPosition = Vector2.zero;
        private static MainTabWindow_ResearchTree _instance;
        public static MainTabWindow_ResearchTree Instance => _instance;

        public MainTabWindow_ResearchTree()
        {
            closeOnClickedOutside = false;
            _instance = this;

        }

        public override void PreClose()
        {
            base.PreClose();
            Log.Debug( "CloseOnClickedOutside: {0}", closeOnClickedOutside );
            Log.Debug( StackTraceUtility.ExtractStackTrace() );
        }

        public override void PreOpen()
        {
            base.PreOpen();

            SetRects();

            // settings changed, notify...
            if (Tree.shouldSeparateByTechLevels != Settings.shouldSeparateByTechLevels)
            {
                Messages.Message(ResourceBank.String.NeedsRestart, MessageTypeDefOf.CautionInput, false);
            }

            if (Settings.shouldPause)
            {
                forcePause = Settings.shouldPause;
            }

            if (Settings.shouldReset)
            {
                _query = "";
                _scrollPosition = Vector2.zero;
                ZoomLevel = 1f;
            }

            // clear node availability caches
            ResearchNode.ClearCaches();

            _dragging = false;
            closeOnClickedOutside = false;
        }

        private void SetRects()
        {
            // tree view rects, have to deal with UIScale and ZoomLevel manually.
            _baseViewRect = new Rect(
                StandardMargin / Prefs.UIScale,
                (TopBarHeight + Constants.Margin + StandardMargin) / Prefs.UIScale,
                (Screen.width - StandardMargin * 2f) / Prefs.UIScale,
                (Screen.height - MainButtonDef.ButtonHeight - StandardMargin * 2f - TopBarHeight - Constants.Margin) / Prefs.UIScale);
            _baseViewRect_Inner = _baseViewRect.ContractedBy( Constants.Margin / Prefs.UIScale );

            // windowrect, set to topleft (for some reason vanilla alignment overlaps bottom buttons).
            windowRect.x = 0f;
            windowRect.y = 0f;
            windowRect.width = UI.screenWidth;
            windowRect.height = UI.screenHeight - MainButtonDef.ButtonHeight;
        }

        public float ScaledMargin => Constants.Margin * ZoomLevel / Prefs.UIScale;

        public override void DoWindowContents( Rect canvas )
        {
            // top bar
            var topRect = new Rect(
                canvas.xMin,
                canvas.yMin,
                canvas.width,
                TopBarHeight );
            DrawTopBar(topRect);
            
            ApplyZoomLevel();

            // draw background
            GUI.DrawTexture( ViewRect, Assets.SlightlyDarkBackground );
            
            // draw the actual tree
            // TODO: stop scrollbars scaling with zoom
            _scrollPosition = GUI.BeginScrollView( ViewRect, _scrollPosition, TreeRect );
            GUI.BeginGroup( 
                new Rect( 
                    ScaledMargin,
                    ScaledMargin,
                    TreeRect.width + ScaledMargin * 2f, 
                    TreeRect.height + ScaledMargin * 2f
                ) 
            );

            Tree.Draw( VisibleRect );
            Queue.DrawLabels( VisibleRect );

            HandleZoom();

            GUI.EndGroup();
            GUI.EndScrollView( false );

            HandleDragging();
            HandleDolly();

            // reset zoom level
            ResetZoomLevel();


            // cleanup;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void HandleDolly()
        {
            var dollySpeed = 10f;
            if ( KeyBindingDefOf.MapDolly_Left.IsDown )
                _scrollPosition.x -= dollySpeed;
            if ( KeyBindingDefOf.MapDolly_Right.IsDown )
                _scrollPosition.x += dollySpeed;
            if ( KeyBindingDefOf.MapDolly_Up.IsDown )
                _scrollPosition.y -= dollySpeed;
            if ( KeyBindingDefOf.MapDolly_Down.IsDown )
                _scrollPosition.y += dollySpeed;
        }


        void HandleZoom()
        {
            // handle zoom only with shift
            if (Event.current.isScrollWheel && Event.current.shift)
            {
                // absolute position of mouse on research tree
                var absPos = Event.current.mousePosition;
                // Log.Debug( "Absolute position: {0}", absPos );

                // relative normalized position of mouse on visible tree
                var relPos = ( Event.current.mousePosition - _scrollPosition ) / ZoomLevel;
                // Log.Debug( "Normalized position: {0}", relPos );
                
                // update zoom level
                ZoomLevel += Event.current.delta.y * ZoomStep * ZoomLevel;
                
                // we want to keep the _normalized_ relative position the same as before zooming
                _scrollPosition = absPos - relPos * ZoomLevel;
            
                Event.current.Use();
            }
        }

        bool _dragging;
        Vector2 _mousePosition = Vector2.zero;
        void HandleDragging()
        {
            // middle mouse or holding down shift for panning
            if (Event.current.button == 2 || Event.current.shift) {
                if (Event.current.type == EventType.mouseDown)
                {
                    _dragging = true;
                    _mousePosition = Event.current.mousePosition;
                    Event.current.Use();
                }
                if (Event.current.type == EventType.mouseUp)
                {
                    _dragging = false;
                    _mousePosition = Vector2.zero;
                }
                if (Event.current.type == EventType.mouseDrag)
                {
                    var _currentMousePosition = Event.current.mousePosition;
                    _scrollPosition += _mousePosition - _currentMousePosition;
                    _mousePosition = _currentMousePosition;
                }
            }
            // scroll wheel vertical, switch to horizontal with alt
            if (Event.current.isScrollWheel && !Event.current.shift) {
                float delta = Event.current.delta.y * 15;
                if (Event.current.alt) {
                    _scrollPosition.x += delta;
                } else {
                    _scrollPosition.y += delta;
                }
            }
        }

        private float _zoomLevel = 1f;
        public float ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                _zoomLevel = Mathf.Clamp(value, 1f, MaxZoomLevel);
                _viewRectDirty = true;
                _viewRect_InnerDirty = true;
            } 
        }

        private Rect _baseViewRect;
        private Rect _baseViewRect_Inner;
        private Rect _viewRect;
        private bool _viewRectDirty = true;
        public Rect ViewRect
        {
            get
            {
                if (_viewRectDirty)
                {
                    _viewRect = new Rect(
                        _baseViewRect.xMin * ZoomLevel,
                        _baseViewRect.yMin * ZoomLevel,
                        _baseViewRect.width * ZoomLevel,
                        _baseViewRect.height * ZoomLevel
                    );
                    _viewRectDirty = false;
                }
                return _viewRect;
            }
        }

        private Rect _viewRect_Inner;
        private bool _viewRect_InnerDirty = true;
        public Rect ViewRect_Inner
        {
            get
            {
                if (_viewRect_InnerDirty)
                {
                    _viewRect_Inner = _viewRect.ContractedBy( Margin * ZoomLevel );
                    _viewRect_InnerDirty = false;
                }
                return _viewRect_Inner;
            }
        }

        private static Rect _treeRect = default( Rect );
        public Rect TreeRect
        {
            get
            {
                if ( _treeRect == default(Rect) )
                {
                    float width = Tree.Size.x * (NodeSize.x + NodeMargins.x);
                    float height = Tree.Size.z * (NodeSize.y + NodeMargins.y);
                    _treeRect = new Rect( 0f, 0f, width, height);
                }
                return _treeRect;
            }
        }

        public Rect VisibleRect
        {
            get
            {
                return new Rect(
                    _scrollPosition.x,
                    _scrollPosition.y,
                    ViewRect_Inner.width,
                    ViewRect_Inner.height);

            }
        }

        internal float MaxZoomLevel
        {
            get
            {
                // get the minimum zoom level at which the entire tree fits onto the screen, or a static maximum zoom level.
                var fitZoomLevel =  Mathf.Max( TreeRect.width / _baseViewRect_Inner.width, TreeRect.height / _baseViewRect_Inner.height );
                return Mathf.Min( fitZoomLevel, AbsoluteMaxZoomLevel );
            }
        }

        private void ApplyZoomLevel()
        {
            GUI.EndClip(); // window contents
            GUI.EndClip(); // window itself?
            GUI.matrix = Matrix4x4.TRS(new Vector3(0f, 0f, 0f), Quaternion.identity, new Vector3( Prefs.UIScale / ZoomLevel, Prefs.UIScale / ZoomLevel, 1f));
        }

        private void ResetZoomLevel()
        {
            // dummies to maintain correct stack size
            // TODO; figure out how to get actual clipping rects in ApplyZoomLevel();
            UI.ApplyUIScale();
            GUI.BeginClip(windowRect);
            GUI.BeginClip( new Rect( 0f, 0f, UI.screenWidth, UI.screenHeight ) );
        }

        private void DrawTopBar( Rect canvas )
        {
            var searchRect = canvas;
            var queueRect = canvas;
            searchRect.width = 200f;
            queueRect.xMin += 200f + Constants.Margin;

            GUI.DrawTexture( searchRect, Assets.SlightlyDarkBackground );
            GUI.DrawTexture( queueRect, Assets.SlightlyDarkBackground );

            DrawSearchBar( searchRect.ContractedBy( Constants.Margin ) );
            Queue.DrawQueue( queueRect.ContractedBy( Constants.Margin ), !_dragging );
        }

        private string _query = "";

        private void DrawSearchBar( Rect canvas )
        {
            Profiler.Start( "DrawSearchBar" );
            var iconRect = new Rect(
                    canvas.xMax - Constants.Margin - 16f,
                    0f,
                    16f,
                    16f )
                .CenteredOnYIn( canvas );
            var searchRect = new Rect(
                    canvas.xMin,
                    0f,
                    canvas.width,
                    30f )
                .CenteredOnYIn( canvas );

            GUI.DrawTexture( iconRect, Assets.Search );
            var query = Widgets.TextField( searchRect, _query );

            if ( query != _query )
            {
                _query = query;
                Find.WindowStack.FloatMenu?.Close( false );

                if ( query.Length > 2 )
                {
                    // open float menu with search results, if any.
                    var options = new List<FloatMenuOption>();

                    foreach ( var result in Tree.Nodes.OfType<ResearchNode>()
                        .Select( n => new { node = n, match = n.Matches( query ) } )
                        .Where( result => result.match > 0 )
                        .OrderBy( result => result.match ) )
                    {
                        options.Add( new FloatMenuOption( result.node.Label, () => CenterOn( result.node ),
                            MenuOptionPriority.Default, () => CenterOn( result.node ) ) );
                    }

                    if ( !options.Any() )
                        options.Add( new FloatMenuOption( ResourceBank.String.NoResearchFound, null ) );

                    Find.WindowStack.Add( new FloatMenu_Fixed( options,
                        UI.GUIToScreenPoint( new Vector2( searchRect.xMin, searchRect.yMax ) ) ) );
                }
            }
            Profiler.End();
        }

        public void CenterOn( Node node )
        {
            var position = new Vector2(
                ( NodeSize.x + NodeMargins.x ) * ( node.X - .5f ),
                ( NodeSize.y + NodeMargins.y ) * ( node.Y - .5f ) );

            node.Highlighted = true;

            position -= new Vector2( UI.screenWidth, UI.screenHeight ) / 2f;

            position.x = Mathf.Clamp( position.x, 0f, TreeRect.width - ViewRect.width );
            position.y = Mathf.Clamp( position.y, 0f, TreeRect.height - ViewRect.height );
            _scrollPosition = position;
        }
    }
}
