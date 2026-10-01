using Xunit;

// 应用里有多处全局静态状态（AppDataHelper 的根目录重定向、Avalonia headless 会话、
// AppSettingsService / DligConfigService 的静态路径），并行执行会互相污染，
// 因此整个测试程序集关闭并行。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
