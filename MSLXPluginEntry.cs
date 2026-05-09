using MSLX.SDK;

namespace MSLX.Plugin.Demo;

public class MSLXPluginEntry : IPlugin
{
    public string Id => "mslx-plugin-rollback"; 
    public string Name => "MSLX 服务器回档插件";
    public string Description => "提供服务器一键回档功能，支持备份检测、存档路径配置、自动启停服务器、倒计时确认等功能";
    public string Version => "1.0.3";
    public string Icon => "icon.png";
    public string MinSDKVersion => "1.4.0";
    public string Developer => "TNT-12M";
    public string AuthorUrl => "https://github.com/TNT-12M";
    public string PluginUrl => "https://github.com/TNT-12M/mslx-plugin-rollback";

    public void OnLoad()
    {
        SDK.MSLX.Logger.Info("mslx-plugin-rollback 载入成功~");
        SDK.MSLX.Logger.Info("当前存在实例数量：" + SDK.MSLX.Config.Servers.GetServerList().Count.ToString());
    }

    public void OnUnload() {
        SDK.MSLX.Logger.Info("mslx-plugin-rollback 卸载成功~");
    }
}