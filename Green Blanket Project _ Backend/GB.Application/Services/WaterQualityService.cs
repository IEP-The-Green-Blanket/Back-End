using System;
using System.Collections.Generic;
using System.Text;
using GB.Application.DTOs;

namespace GB.Application.Services
{
    public class WaterQualityService
    {
        public string GetRandomStatus()
        {
            var statuses = new List<string> { "Safe", "Neutral", "Unsafe" };

            Random random = new Random();
            int index = random.Next(statuses.Count);

            return statuses[index];

        }

        public WaterChemicalsDto GetRandomChemicals()
        {
            Random random = new Random();

            return new WaterChemicalsDto
            {
                Nitrate = Math.Round(random.NextDouble() * 10, 2),   
                Phosphate = Math.Round(random.NextDouble() * 5, 2), 
                Oxygen = Math.Round(random.NextDouble() * 12, 2)    
            };
        }
    }
         
}