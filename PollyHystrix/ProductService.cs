using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PollyHystrix
{
    public class ProductService
    {
        [HystrixCommand(nameof(GetAllProductsFallBackAsync),
            IsEnableCircuitBreaker = true,
            ExceptionsAllowedBeforeBreaking = 3,
            MillisecondsOfBreak = 1000 * 5)]
        public virtual async Task<string> GetAllProductsAsync(string productType)
        {
            Console.WriteLine($"-->>Starting get product type : {productType}");
            string str = null;
            str.ToString();

            await Task.Delay(30);

            if (DateTime.Now.Second % 2 == 0)
                throw new Exception("发生异常");
            // to do : using HttpClient to call outer service to get product list

            return $"OK {productType}";
        }

        public virtual async Task<string> GetAllProductsFallBackAsync(string productType)
        {
            Console.WriteLine($"-->>FallBack : Starting get product type : {productType}");
            await Task.Delay(30);
            return $"OK for FallBack  {productType}";
        }
    }
}
