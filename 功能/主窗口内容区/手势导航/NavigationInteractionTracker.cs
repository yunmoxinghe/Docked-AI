// Copyright (c) Files Community
// Licensed under the MIT License.
//
// 触摸板双指前进后退手势导航
// 使用方式见 README.md

using System;
using System.Collections.Generic;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.Interactions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace GestureNavigation
{
    /// <summary>
    /// 触摸板双指前进后退手势跟踪器
    /// 提供类似浏览器的触摸板手势导航功能
    /// </summary>
    public sealed partial class NavigationInteractionTracker : IDisposable
    {
        /// <summary>
        /// 是否可以向前导航
        /// </summary>
        public bool CanNavigateForward
        {
            get
            {
                _props.TryGetBoolean(nameof(CanNavigateForward), out bool val);
                return val;
            }
            set
            {
                if (!_disposed)
                {
                    _props.InsertBoolean(nameof(CanNavigateForward), value);
                    _tracker.MaxPosition = new(value ? 96f : 0f);
                }
            }
        }

        /// <summary>
        /// 是否可以向后导航
        /// </summary>
        public bool CanNavigateBackward
        {
            get
            {
                _props.TryGetBoolean(nameof(CanNavigateBackward), out bool val);
                return val;
            }
            set
            {
                if (!_disposed)
                {
                    _props.InsertBoolean(nameof(CanNavigateBackward), value);
                    _tracker.MinPosition = new(value ? -96f : 0f);
                }
            }
        }

        /// <summary>
        /// 后退指示器显示的图标字符（Segoe Fluent Icons glyph）
        /// </summary>
        public string? BackPageIcon
        {
            get => _backPageIcon;
            set
            {
                if (!_disposed && _backPageIcon != value)
                {
                    _backPageIcon = value;
                    UpdateBackIcon(value);
                }
            }
        }

        /// <summary>
        /// 前进指示器显示的图标字符（Segoe Fluent Icons glyph）
        /// </summary>
        public string? ForwardPageIcon
        {
            get => _forwardPageIcon;
            set
            {
                if (!_disposed && _forwardPageIcon != value)
                {
                    _forwardPageIcon = value;
                    UpdateForwardIcon(value);
                }
            }
        }

        private readonly UIElement _rootElement;
        private readonly UIElement _backIcon;
        private readonly UIElement _forwardIcon;
        private TextBlock? _backTextBlock;
        private TextBlock? _forwardTextBlock;
        private string? _backPageIcon;
        private string? _forwardPageIcon;

        private readonly Visual _rootVisual;
        private readonly Visual _backVisual;
        private readonly Visual _forwardVisual;

        private InteractionTracker _tracker;
        private VisualInteractionSource _source;
        private InteractionTrackerOwner _trackerOwner;
        private readonly CompositionPropertySet _props;

        /// <summary>
        /// 导航请求事件，订阅后在手势触发时执行前进/后退
        /// </summary>
        public event EventHandler<OverscrollNavigationDirection>? NavigationRequested;

        private bool _disposed;

        /// <summary>
        /// 初始化手势跟踪器
        /// </summary>
        /// <param name="rootElement">手势捕获的根容器（包含 Frame 的 Grid）</param>
        /// <param name="backIcon">后退指示器 Border 元素</param>
        /// <param name="forwardIcon">前进指示器 Border 元素</param>
        public NavigationInteractionTracker(UIElement rootElement, UIElement backIcon, UIElement forwardIcon)
        {
            _rootElement = rootElement;
            _backIcon = backIcon;
            _forwardIcon = forwardIcon;

            _backTextBlock = FindChild<TextBlock>(_backIcon);
            _forwardTextBlock = FindChild<TextBlock>(_forwardIcon);

            ElementCompositionPreview.SetIsTranslationEnabled(_backIcon, true);
            ElementCompositionPreview.SetIsTranslationEnabled(_forwardIcon, true);

            _rootVisual = ElementCompositionPreview.GetElementVisual(_rootElement);
            _backVisual = ElementCompositionPreview.GetElementVisual(_backIcon);
            _forwardVisual = ElementCompositionPreview.GetElementVisual(_forwardIcon);

            SetupInteractionTracker();

            _props = _rootVisual.Compositor.CreatePropertySet();
            CanNavigateBackward = false;
            CanNavigateForward = false;
            _backPageIcon = string.Empty;
            _forwardPageIcon = string.Empty;

            SetupAnimations();
        }

        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result) return result;
                var descendant = FindChild<T>(child);
                if (descendant != null) return descendant;
            }
            return null;
        }

        private void UpdateBackIcon(string? iconGlyph)
        {
            if (_backTextBlock != null && !string.IsNullOrEmpty(iconGlyph))
                _backTextBlock.Text = iconGlyph;
        }

        private void UpdateForwardIcon(string? iconGlyph)
        {
            if (_forwardTextBlock != null && !string.IsNullOrEmpty(iconGlyph))
                _forwardTextBlock.Text = iconGlyph;
        }

        [MemberNotNull(nameof(_tracker), nameof(_source), nameof(_trackerOwner))]
        private void SetupInteractionTracker()
        {
            var compositor = _rootVisual.Compositor;

            _trackerOwner = new(this);
            _tracker = InteractionTracker.CreateWithOwner(compositor, _trackerOwner);
            _tracker.MinPosition = new Vector3(-96f);
            _tracker.MaxPosition = new Vector3(96f);

            _source = VisualInteractionSource.Create(_rootVisual);
            _source.ManipulationRedirectionMode = VisualInteractionSourceRedirectionMode.CapableTouchpadAndPointerWheel;
            _source.PositionXSourceMode = InteractionSourceMode.EnabledWithoutInertia;
            _source.PositionXChainingMode = InteractionChainingMode.Always;
            _source.PositionYSourceMode = InteractionSourceMode.Disabled;
            _source.IsPositionXRailsEnabled = false;
            _source.IsPositionYRailsEnabled = true;

            _tracker.InteractionSources.Add(_source);
        }

        private void SetupAnimations()
        {
            var compositor = _rootVisual.Compositor;

            var backResistance = CreateResistanceCondition(-96f, 0f);
            var forwardResistance = CreateResistanceCondition(0f, 96f);
            _source.ConfigureDeltaPositionXModifiers([backResistance, forwardResistance]);

            // 后退指示器：初始在屏幕外左侧，向右滑入
            var backAnim = compositor.CreateExpressionAnimation("(-clamp(tracker.Position.X, -96, 0) * 2) - 100");
            backAnim.SetReferenceParameter("tracker", _tracker);
            _backVisual.StartAnimation("Translation.X", backAnim);

            // 前进指示器：初始在屏幕外右侧，向左滑入
            var forwardAnim = compositor.CreateExpressionAnimation("(-clamp(tracker.Position.X, 0, 96) * 2) + 100");
            forwardAnim.SetReferenceParameter("tracker", _tracker);
            _forwardVisual.StartAnimation("Translation.X", forwardAnim);
        }

        private CompositionConditionalValue CreateResistanceCondition(float minValue, float maxValue)
        {
            var compositor = _rootVisual.Compositor;
            var resistance = CompositionConditionalValue.Create(compositor);

            var condition = compositor.CreateExpressionAnimation(
                $"tracker.Position.X > {minValue} && tracker.Position.X < {maxValue}");
            condition.SetReferenceParameter("tracker", _tracker);

            var value = compositor.CreateExpressionAnimation(
                $"source.DeltaPosition.X * (1 - sqrt(1 - square((tracker.Position.X / {minValue + maxValue}) - 1)))");
            value.SetReferenceParameter("source", _source);
            value.SetReferenceParameter("tracker", _tracker);

            resistance.Condition = condition;
            resistance.Value = value;
            return resistance;
        }

        ~NavigationInteractionTracker() => Dispose();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _backVisual.StopAnimation("Translation.X");
            _forwardVisual.StopAnimation("Translation.X");
            _tracker.Dispose();
            _source.Dispose();
            _props.Dispose();
            GC.SuppressFinalize(this);
        }

        // ── InteractionTrackerOwner ──────────────────────────────────────────────

        private sealed partial class InteractionTrackerOwner : IInteractionTrackerOwner
        {
            private readonly NavigationInteractionTracker _parent;
            private bool _shouldBounceBack;
            private bool _shouldAnimate = true;
            private readonly Vector3KeyFrameAnimation _scaleAnimation;
            private readonly SpringVector3NaturalMotionAnimation _returnAnimation;

            public InteractionTrackerOwner(NavigationInteractionTracker parent)
            {
                _parent = parent;
                var compositor = _parent._rootVisual.Compositor;

                _scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
                _scaleAnimation.InsertKeyFrame(0.5f, new(1.3f));
                _scaleAnimation.InsertKeyFrame(1f, new(1f));
                _scaleAnimation.Duration = TimeSpan.FromMilliseconds(275);

                // 56x56 指示器的中心点
                _parent._backVisual.CenterPoint = new Vector3(28, 28, 0);
                _parent._forwardVisual.CenterPoint = new Vector3(28, 28, 0);

                _returnAnimation = compositor.CreateSpringVector3Animation();
                _returnAnimation.FinalValue = new(0f);
                _returnAnimation.DampingRatio = 1f;
            }

            public void IdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
            {
                if (!_shouldBounceBack) return;
                try
                {
                    if (Math.Abs(sender.Position.X) > 64)
                    {
                        _parent._tracker.TryUpdatePosition(new(0f));
                        var navEvent = _parent.NavigationRequested;
                        if (navEvent is not null)
                        {
                            if (sender.Position.X > 0 && _parent.CanNavigateForward)
                                navEvent(_parent, OverscrollNavigationDirection.Forward);
                            else if (sender.Position.X < 0 && _parent.CanNavigateBackward)
                                navEvent(_parent, OverscrollNavigationDirection.Back);
                        }
                    }
                    else
                    {
                        _parent._tracker.TryUpdatePositionWithAnimation(_returnAnimation);
                    }
                }
                catch
                {
                    try { _parent._tracker.TryUpdatePosition(new(0f)); } catch { }
                }
                finally
                {
                    _shouldBounceBack = false;
                    _shouldAnimate = true;
                }
            }

            public void InteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
                => _shouldBounceBack = true;

            public void ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
            {
                if (!_shouldAnimate) return;
                if (args.Position.X <= -64)
                {
                    _parent._backVisual.StartAnimation("Scale", _scaleAnimation);
                    _shouldAnimate = false;
                }
                else if (args.Position.X >= 64)
                {
                    _parent._forwardVisual.StartAnimation("Scale", _scaleAnimation);
                    _shouldAnimate = false;
                }
            }

            public void CustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args) { }
            public void InertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args) { }
            public void RequestIgnored(InteractionTracker sender, InteractionTrackerRequestIgnoredArgs args) { }
        }
    }

    /// <summary>
    /// 手势导航方向
    /// </summary>
    public enum OverscrollNavigationDirection
    {
        Back,
        Forward
    }
}
