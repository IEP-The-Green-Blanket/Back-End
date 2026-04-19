using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GB.Domain.Entities
{
    [Table("water_data", Schema = "hartbeespoortdam")]
    public class WaterReading
    {
        [Column("mon_feature_id")]
        public int? MonFeatureId { get; set; }

        [Column("date_time")]
        public DateTime DateTime { get; set; }

        [Column("ph_diss_water")]
        public double? PhLevel { get; set; }

        [Column("ec_phys_water")]
        public double? ElectricalConductivity { get; set; }

        [Column("no3_no2_n_diss_water")]
        public double? Nitrates { get; set; }

        [Column("po4_p_diss_water")]
        public double? Phosphates { get; set; }

        [Column("nh4_n_diss_water")]
        public double? Ammonia { get; set; }

        [Column("ca_diss_water")]
        public double? Calcium { get; set; }

        [Column("mg_diss_water")]
        public double? Magnesium { get; set; }

        [Column("na_diss_water")]
        public double? Sodium { get; set; }
        [Column("cl_diss_water")]
        public double? Chloride { get; set; }

        [Column("so4_diss_water")]
        public double? Sulfate { get; set; }

        [Column("tal_diss_water")]
        public double? TotalAlkalinity { get; set; }

        [Column("kjel_n_tot_water")]
        public double? KjeldahlNitrogen { get; set; }

        [Column("qat")]
        public string? QualityTag { get; set; }
    }
}