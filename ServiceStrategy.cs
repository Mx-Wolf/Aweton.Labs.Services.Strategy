using System.Diagnostics;
using Aweton.Labs.Services.Abstraction;
using Microsoft.Extensions.Logging;
namespace Aweton.Labs.Services.Strategy;
public class ServiceStrategy<TInit, TTask, TResponse, TResult> : ITaskStrategy
{
  private readonly ITaskInit<TInit> m_Init;
  private readonly ITaskSourceFactory<TInit, TTask, TResult> m_Factory;
  private readonly ITaskRunner<TTask, TResponse> m_Runner;
  private readonly ITaskAdapter<TResponse, TResult> m_Adapter;
  private readonly ILogger<ServiceStrategy<TInit, TTask, TResponse, TResult>> m_Logger;

  public ServiceStrategy(
    ITaskSourceFactory<TInit, TTask, TResult> factory,
    ITaskRunner<TTask, TResponse> runner,
    ITaskAdapter<TResponse, TResult> adapter,
    ILogger<ServiceStrategy<TInit, TTask, TResponse, TResult>> logger,
    ITaskInit<TInit> init)
  {
    m_Factory = factory;
    m_Runner = runner;
    m_Adapter = adapter;
    m_Logger = logger;
    m_Init = init;
  }

  public async Task Run()
  {
    var timer = new Stopwatch();
    timer.Start();
    try
    {
      m_Logger.LogTrace("begin run");
      await RunUnderTryCatch();
      timer.Stop();
      m_Logger.LogTrace($"end run: elapsed: ${FormatDuration(timer.Elapsed)}");
    }
    catch (Exception ex)
    {
      timer.Stop();
      m_Logger.LogError(ex, $"Run failed: elapsed {FormatDuration(timer.Elapsed)}");
    }
  }

  private async Task RunUnderTryCatch() => await RunSource(await m_Factory.CreateSource(await m_Init.ComputeInit()));

  private async Task RunSource(ITaskSource<TTask, TResult> source)
  {
    var list = await source.LoadTasks();
    try
    {
      await Task.WhenAll(
        list.Select(
          async (task) => source.RegisterResult(
            await m_Adapter.Transform(
              await m_Runner.ExecuteTask(task)
            )
          )
        )
      );
      await source.RegisterSuccess();
    }
    catch (Exception ex)
    {
      await source.RegisterFailure(ex);
      throw;
    }
  }

  private string FormatDuration(TimeSpan elapsed)
  {
    return $"{elapsed.Hours:D2}:${elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds:D3}";
  }
}
