using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Crawl_Ma_So_The
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Crawl_Ma_So_Thue crawl_Ma_So_Thue=new Crawl_Ma_So_Thue();
            for (int i = 0; i <= 1000; ++i)
            {
                var PageIndex = i;
                ThreadController threadController = new ThreadController();
                var cancelToken=new CancellationTokenSource();
                var pauseToken = new PauseTokenSource();
                threadController.StartTask(async() =>
                {
                    while(true)
                    {
                        try
                        {
                            cancelToken.Token.ThrowIfCancellationRequested();
                        }
                        catch 
                        {
                            return;
                        }
                        await pauseToken.Token.PauseIfRequestedAsync();
                        crawl_Ma_So_Thue.Craw_Ma_So_Thue_Theo_Tinh("an-giang-93", PageIndex);
                    }
                }, cancelToken, pauseToken);
                Thread.Sleep(5000);
                threadController.PauseTask();
                Thread.Sleep(5000);
                threadController.ResumeTask();
                Thread.Sleep(3000);
                threadController.StopTask();
            }
            Console.ReadKey();
        }
    }
    public class Crawl_Ma_So_Thue
    {
        public void Craw_Ma_So_Thue_Theo_Tinh(string tinh, int PageNumber)
        {
            using (WebClient webClient = new WebClient())
            {
                webClient.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36";
                webClient.Encoding = Encoding.UTF8;
                var html = webClient.DownloadString($"https://masothue.com/tra-cuu-ma-so-thue-theo-tinh/{tinh}?page={PageNumber}");
                HtmlDocument htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(html);
                var elements=htmlDocument.DocumentNode.Descendants("div").FirstOrDefault(d=>d.Attributes.Contains("class")&&d.Attributes["class"].Value.Contains("tax-listing"));
                if(elements != null)
                {
                    var ctys = elements.Descendants("div").Where(d => d.Attributes.Contains("data-prefetch"));
                    foreach(var cty in ctys)
                    {
                        var ases = cty.Descendants("a");
                        var address = cty.Descendants("address");
                        string ctyData = $"CTY: {ases.ElementAt(0).InnerText} - MST: {ases.ElementAt(1).InnerText} - Người đại diện: {ases.ElementAt(2).InnerText} - Địa chỉ: {address.ElementAt(0).InnerText}\n";
                        Console.WriteLine(ctyData);
                    }
                }
                else
                {
                    Console.WriteLine("Không có data");
                }
            }
        }
    }
    public class ThreadController
    {
        public delegate void ThreadAction();
        public Task _Task;
        CancellationTokenSource src;
        PauseTokenSource pauseSource;
        public bool StopTask()
        {
            if (_Task == null)
                return true;
            try
            {
                if (src == null)
                    return true;
                src.Cancel();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void TaskWait(CancellationTokenSource TokenSrouce, PauseTokenSource PauseSource)
        {
            var ct = StartTask(async () =>
            {

                while (true)
                {
                    try
                    {
                        TokenSrouce.Token.ThrowIfCancellationRequested();
                    }
                    catch
                    {
                        return;
                    }
                    await PauseSource.Token.PauseIfRequestedAsync();

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }, TokenSrouce, PauseSource);
        }

        public bool StartTask(ThreadAction action, CancellationTokenSource TokenSrouce, PauseTokenSource PauseSource)
        {
            if (_Task != null)
            {
                StopTask();
                _Task = null;
            }
            try
            {
                src = TokenSrouce;
                pauseSource = PauseSource;
                if (TokenSrouce == null)
                {
                    _Task = Task.Run(() => { action(); });
                }
                else
                {
                    _Task = Task.Run(() => { action(); }, TokenSrouce.Token);
                }



                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> PauseTask()
        {
            if (_Task == null)
                return true;

            try
            {
                await pauseSource.PauseAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ResumeTask()
        {
            if (_Task == null)
                return true;

            try
            {
                await pauseSource.ResumeAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
    public class PauseTokenSource
    {
        bool _paused = false;
        bool _pauseRequested = false;

        TaskCompletionSource<bool> _resumeRequestTcs;
        TaskCompletionSource<bool> _pauseConfirmationTcs;

        readonly SemaphoreSlim _stateAsyncLock = new SemaphoreSlim(1);
        readonly SemaphoreSlim _pauseRequestAsyncLock = new SemaphoreSlim(1);

        public PauseToken Token { get { return new PauseToken(this); } }

        public async Task<bool> IsPaused(CancellationToken token = default(CancellationToken))
        {
            await _stateAsyncLock.WaitAsync(token);
            try
            {
                return _paused;
            }
            finally
            {
                _stateAsyncLock.Release();
            }
        }

        public async Task ResumeAsync(CancellationToken token = default(CancellationToken))
        {
            await _stateAsyncLock.WaitAsync(token);
            try
            {
                if (!_paused)
                {
                    return;
                }

                await _pauseRequestAsyncLock.WaitAsync(token);
                try
                {
                    var resumeRequestTcs = _resumeRequestTcs;
                    _paused = false;
                    _pauseRequested = false;
                    _resumeRequestTcs = null;
                    _pauseConfirmationTcs = null;
                    resumeRequestTcs.TrySetResult(true);
                }
                finally
                {
                    _pauseRequestAsyncLock.Release();
                }
            }
            finally
            {
                _stateAsyncLock.Release();
            }
        }

        public async Task PauseAsync(CancellationToken token = default(CancellationToken))
        {
            await _stateAsyncLock.WaitAsync(token);
            try
            {
                if (_paused)
                {
                    return;
                }

                Task pauseConfirmationTask = null;

                await _pauseRequestAsyncLock.WaitAsync(token);
                try
                {
                    _pauseRequested = true;
                    _resumeRequestTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _pauseConfirmationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    pauseConfirmationTask = WaitForPauseConfirmationAsync(token);
                }
                finally
                {
                    _pauseRequestAsyncLock.Release();
                }

                await pauseConfirmationTask;

                _paused = true;
            }
            finally
            {
                _stateAsyncLock.Release();
            }
        }

        private async Task WaitForResumeRequestAsync(CancellationToken token)
        {
            using (token.Register(() => _resumeRequestTcs.TrySetCanceled(), useSynchronizationContext: false))
            {
                await _resumeRequestTcs.Task;
            }
        }

        private async Task WaitForPauseConfirmationAsync(CancellationToken token)
        {
            using (token.Register(() => _pauseConfirmationTcs.TrySetCanceled(), useSynchronizationContext: false))
            {
                await _pauseConfirmationTcs.Task;
            }
        }

        internal async Task PauseIfRequestedAsync(CancellationToken token = default(CancellationToken))
        {
            Task resumeRequestTask = null;

            await _pauseRequestAsyncLock.WaitAsync(token);
            try
            {
                if (!_pauseRequested)
                {
                    return;
                }
                resumeRequestTask = WaitForResumeRequestAsync(token);
                _pauseConfirmationTcs.TrySetResult(true);
            }
            finally
            {
                _pauseRequestAsyncLock.Release();
            }

            await resumeRequestTask;
        }
    }

    // PauseToken - consumer side
    public struct PauseToken
    {
        readonly PauseTokenSource _source;

        public PauseToken(PauseTokenSource source) { _source = source; }

        public Task<bool> IsPaused() { return _source.IsPaused(); }

        public Task PauseIfRequestedAsync(CancellationToken token = default(CancellationToken))
        {
            return _source.PauseIfRequestedAsync(token);
        }
    }
}
