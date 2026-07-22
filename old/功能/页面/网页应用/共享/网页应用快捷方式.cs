namespace Docked_AI.Features.Pages.WebApp.Shared
{
    public sealed record WebAppShortcut(
        string Id, 
        string Name, 
        string Url, 
        byte[]? IconBytes,
        KeyboardMappingButtonConfig? LeftButtonConfig = null,
        KeyboardMappingButtonConfig? RightButtonConfig = null)
    {
        /// <summary>
        /// 获取左侧按钮配置（如果为 null 则返回默认禁用配置）
        /// </summary>
        public KeyboardMappingButtonConfig LeftButton => LeftButtonConfig ?? KeyboardMappingButtonConfig.CreateDefault();

        /// <summary>
        /// 获取右侧按钮配置（如果为 null 则返回默认禁用配置）
        /// </summary>
        public KeyboardMappingButtonConfig RightButton => RightButtonConfig ?? KeyboardMappingButtonConfig.CreateDefault();
    }
}
