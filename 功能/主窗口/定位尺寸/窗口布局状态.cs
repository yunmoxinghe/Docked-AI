using Docked_AI.Features.MainWindow.Placement;

namespace Docked_AI.Features.MainWindow.Placement
{
    /// <summary>
    /// 窗口布局状态 - 存储窗口位置、尺寸和屏幕信息
    /// 
    /// 【文件职责】
    /// 1. 作为布局信息的数据容器
    /// 2. 存储屏幕尺寸、工作区、窗口尺寸、位置信息
    /// 3. 提供动画控制器和布局服务共享的数据结构
    /// 
    /// 【核心设计】
    /// 
    /// 为什么需要单独的状态类？
    /// - 数据封装：将布局相关的数据集中管理
    /// - 共享数据：多个服务（布局服务、动画控制器）共享同一状态
    /// - 可测试性：可以独立创建和测试布局状态
    /// 
    /// 【属性说明】
    /// 
    /// 屏幕信息：
    /// - ScreenWidth: 屏幕宽度（像素）
    /// - ScreenHeight: 屏幕高度（像素）
    /// - WorkArea: 工作区矩形（不包括任务栏）
    /// 
    /// 窗口尺寸：
    /// - WindowWidth: 窗口宽度（像素）
    /// - WindowHeight: 窗口高度（像素）
    /// - MinWindowWidth: 最小窗口宽度（380px，确保 UI 不被压缩）
    /// 
    /// 窗口位置：
    /// - TargetX: 目标 X 坐标（动画结束位置）
    /// - TargetY: 目标 Y 坐标（动画结束位置）
    /// - CurrentX: 当前 X 坐标（动画执行期间实时更新）
    /// - CurrentY: 当前 Y 坐标（通常等于 TargetY）
    /// 
    /// 布局参数：
    /// - Margin: 窗口边距（10px，标准模式下窗口与屏幕边缘的距离）
    /// 
    /// 【使用场景】
    /// - WindowLayoutService: 计算和更新布局状态
    /// - SlideAnimationController: 读取起始位置和目标位置，更新当前位置
    /// - WindowHostController: 读取布局状态，设置窗口位置
    /// 
    /// 【重构风险点】
    /// 1. 属性类型：
    ///    - TargetX/CurrentX 使用 int/double 类型
    ///    - 如果修改类型，需要同步修改所有使用方
    /// 2. 默认值：
    ///    - MinWindowWidth 默认 380px
    ///    - Margin 默认 10px
    ///    - 如果修改默认值，需要考虑 UI 布局的适配
    /// 3. WorkArea 类型：
    ///    - 使用 PlacementWin32Api.RECT 结构
    ///    - 如果修改为其他类型，需要更新 Win32 API 调用
    /// </summary>
    internal sealed class WindowLayoutState
    {
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public PlacementWin32Api.RECT WorkArea;
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public int MinWindowWidth { get; set; } = 380;
        public int Margin { get; set; } = 10;
        public int TargetX { get; set; }
        public double TargetY { get; set; }
        public double CurrentX { get; set; }
        public double CurrentY { get; set; }
    }
}
