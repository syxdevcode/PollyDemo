using Polly;
using Polly.Caching.Memory;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Polly.Utilities;
using Polly.Wrap;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PollyDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                var key = Console.ReadLine();
                int num = 0;
                if (key != null)
                    int.TryParse(key.ToString(), out num);

                switch (num)
                {
                    case 1:
                        RetryTest();
                        break;

                    case 2:
                        CircuitBreaker();
                        break;

                    case 3:
                        AdvancedCircuitBreaker();
                        break;

                    case 4:
                        FallbackTest();
                        break;

                    case 5:
                        FallbackWrap();
                        break;

                    case 6:
                        PessimisticTimeOutTest();
                        break;
                    case 7:

                        break;
                }
            }
        }

        /// <summary>
        /// 重试
        /// </summary>
        static void RetryTest()
        {
            try
            {
                ISyncPolicy policy = Policy.Handle<ArgumentException>()
                .Retry(2, (ex, retryCount, context) =>
                 {
                     Console.WriteLine($"Error occured,runing fallback,exception :{ex.Message},retryCount:{retryCount}");
                 });

                policy.Execute(() =>
                {
                    Console.WriteLine("Job Start");

                    throw new ArgumentException("Hello Polly!");
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("There's one unhandled exception : " + ex.Message);
            }
        }

        static void CircuitBreaker()
        {
            Action<Exception, TimeSpan, Context> onBreak = (exception, timespan, context) =>
            {
                Console.WriteLine("onBreak Running : " + exception.Message);
            };

            Action<Context> onReset = context =>
            {
                Console.WriteLine("Job Back to normal");
            };

            CircuitBreakerPolicy breaker = Policy
                .Handle<AggregateException>()
                .CircuitBreaker(3, TimeSpan.FromSeconds(10), onBreak, onReset);

            // Monitor the circuit state, for example for health reporting.
            CircuitState state = breaker.CircuitState;

            ISyncPolicy policy = Policy.Handle<ArgumentException>()
                .Retry(3, (ex, retryCount, context) =>
                {
                    Console.WriteLine($"Runing fallback,Exception :{ex.Message},RetryCount:{retryCount}");
                });

            while (true)
            {
                try
                {
                    var policyWrap = Policy.Wrap(policy, breaker);

                    // (wraps the policies around any executed delegate: fallback outermost ... bulkhead innermost)
                    policyWrap.Execute(() =>
                    {
                        Console.WriteLine("Job Start");

                        if (DateTime.Now.Second % 3 == 0)
                            throw new ArgumentException("Hello Polly!");
                    });
                }
                catch (Exception ex)
                {
                    // 手动打开熔断器，阻止执行
                    breaker.Isolate();
                }
                Thread.Sleep(1000);

                // 恢复操作，启动执行
                breaker.Reset();
            }
        }

        static void AdvancedCircuitBreaker()
        {
            Action<Exception, TimeSpan, Context> onBreak = (exception, timespan, context) =>
            {
                Console.WriteLine("onBreak Running,the exception.Message : " + exception.Message);
            };

            Action<Context> onReset = context =>
            {
                Console.WriteLine("Job Back to normal");
            };

            var breaker = Policy.Handle<ArgumentException>()
                .AdvancedCircuitBreaker(
                    failureThreshold: 0.5, // Break on >=50% actions result in handled exceptions...
                    samplingDuration: TimeSpan.FromSeconds(3), // ... over any 10 second period
                    minimumThroughput: 3, // ... provided at least 8 actions in the 10 second period.
                    durationOfBreak: TimeSpan.FromSeconds(5), // Break for 30 seconds.
                    onBreak: onBreak, onReset: onReset);

            ISyncPolicy policy = Policy.Handle<ArgumentException>()
                .WaitAndRetry(new[]{ TimeSpan.FromMilliseconds(200),
                    TimeSpan.FromMilliseconds(400)}, (ex, timesapn, context) =>
                {
                    // Monitor the circuit state, for example for health reporting.
                    CircuitState state = breaker.CircuitState;
                    Console.WriteLine(state.ToString());

                    Console.WriteLine($"Runing Retry,Exception :{ex.Message},timesapn:{timesapn}");
                });

            while (true)
            {
                try
                {
                    var policyWrap = Policy.Wrap(policy, breaker);

                    // (wraps the policies around any executed delegate: fallback outermost ... bulkhead innermost)
                    policyWrap.Execute(() =>
                    {
                        Console.WriteLine("Job Start");

                        if (DateTime.Now.Second % 2 == 0)
                            throw new ArgumentException("Hello Polly!");
                    });
                }
                catch (Exception ex)
                {
                    // 手动打开熔断器，阻止执行
                    //breaker.Isolate();
                }
                Thread.Sleep(500);

                // 恢复操作，启动执行
                //breaker.Reset();
            }
        }

        /// <summary>
        /// 降级
        /// </summary>
        static void FallbackTest()
        {
            ISyncPolicy policy = Policy.Handle<ArgumentException>()
            .Fallback(() =>
            {
                Console.WriteLine("Error occured,runing fallback");
            });

            policy.Execute(() =>
            {
                Console.WriteLine("Job Start");

                throw new ArgumentException("Hello Polly!");
            });
        }

        static void FallbackWrap()
        {
            Action<Exception, TimeSpan, Context> onBreak = (exception, timespan, context) =>
            {
                Console.WriteLine("onBreak Running,the exception.Message : " + exception.Message);
            };

            Action<Context> onReset = context =>
            {
                Console.WriteLine("Job Back to normal");
            };

            ISyncPolicy fallback = Policy.Handle<Polly.CircuitBreaker.BrokenCircuitException>()
                .Or<System.ArgumentException>()
                .Fallback(() =>
                {
                    Console.WriteLine("Error occured,runing fallback");
                },
                (ex) => { Console.WriteLine($"Fallback exception:{ex.GetType().ToString()},Message:{ex.Message}"); });

            ISyncPolicy retry = Policy.Handle<ArgumentException>()
                .WaitAndRetry(new[]{ TimeSpan.FromMilliseconds(200),
                    TimeSpan.FromMilliseconds(300)}, (ex, timesapn, context) =>
                    {
                        Console.WriteLine($"Runing Retry,Exception :{ex.Message},timesapn:{timesapn}");
                    });

            var breaker = Policy.Handle<ArgumentException>()
                .AdvancedCircuitBreaker(
                    failureThreshold: 0.5, // Break on >=50% actions result in handled exceptions...
                    samplingDuration: TimeSpan.FromSeconds(3), // ... over any 10 second period
                    minimumThroughput: 3, // ... provided at least 8 actions in the 10 second period.
                    durationOfBreak: TimeSpan.FromSeconds(8), // Break for 30 seconds.
                    onBreak: onBreak, onReset: onReset);

            // Monitor the circuit state, for example for health reporting.
            CircuitState state = breaker.CircuitState;
            Console.WriteLine(state.ToString());

            while (true)
            {
                var policyWrap = Policy.Wrap(fallback, retry, breaker);

                // (wraps the policies around any executed delegate: fallback outermost ... bulkhead innermost)
                policyWrap.Execute(() =>
                {
                    Console.WriteLine("Job Start");

                    if (DateTime.Now.Second % 2 == 0)
                        throw new ArgumentException("Hello Polly!");
                });

                Thread.Sleep(300);
            }
        }

        /// <summary>
        /// 悲观超时
        /// </summary>
        static void PessimisticTimeOutTest()
        {
            CancellationTokenSource userCancellationSource = new CancellationTokenSource();

            ISyncPolicy fallback = Policy.Handle<Polly.Timeout.TimeoutRejectedException>()
                 .Or<ArgumentException>()
                 .Fallback(() =>
                 {
                     Console.WriteLine("Error occured,runing fallback");
                 },
                 (ex) => { Console.WriteLine($"Fallback exception:{ex.GetType().ToString()},Message:{ex.Message}"); });

            ISyncPolicy policyTimeout = Policy.Timeout(3, Polly.Timeout.TimeoutStrategy.Pessimistic);

            while (true)
            {
                var policyWrap = Policy.Wrap(fallback, policyTimeout);

                // (wraps the policies around any executed delegate: fallback outermost ... bulkhead innermost)
                policyWrap.Execute(() =>
                {
                    Console.WriteLine("Job Start");

                    if (DateTime.Now.Second % 2 == 0)
                    {
                        Thread.Sleep(3000);
                        throw new ArgumentException("Hello Polly!");
                    }
                });
                Thread.Sleep(300);
            }
        }

    }

}
