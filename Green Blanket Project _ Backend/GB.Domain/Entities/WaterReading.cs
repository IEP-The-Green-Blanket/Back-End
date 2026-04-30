using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace GB.Domain.Entities
{
    [Table("water_data", Schema = "hartbeespoortdam")]
    public class WaterReading
    {
        // --- PRIMARY KEYS (Configured via Fluent API in DbContext) ---
        [Column("mon_feature_id")]
        public int MonFeatureId { get; set; }

        [Column("date_time")]
        public DateTime DateTime { get; set; }

        // --- METADATA ---
        [Column("sample_begin_depth")]
        public double? SampleBeginDepth { get; set; }

        [Column("institution_abbr")]
        public string? InstitutionAbbr { get; set; }

        [Column("preservative_abbr")]
        public string? PreservativeAbbr { get; set; }

        // --- CHEMICAL READINGS & DETECTION LIMITS (_dl) ---

        [Column("ca_diss_water")]
        public double? Calcium { get; set; }
        [Column("ca_diss_water_dl")]
        public double? CalciumDl { get; set; }

        [Column("cl_diss_water")]
        public double? Chloride { get; set; }
        [Column("cl_diss_water_dl")]
        public double? ChlorideDl { get; set; }

        [Column("dms_tot_water")]
        public double? DmsTotal { get; set; }
        [Column("dms_tot_water_dl")]
        public double? DmsTotalDl { get; set; }

        [Column("ec_phys_water")]
        public double? ElectricalConductivity { get; set; }
        [Column("ec_phys_water_dl")]
        public double? ElectricalConductivityDl { get; set; }

        [Column("f_diss_water")]
        public double? Fluoride { get; set; }
        [Column("f_diss_water_dl")]
        public double? FluorideDl { get; set; }

        [Column("k_diss_water")]
        public double? Potassium { get; set; }
        [Column("k_diss_water_dl")]
        public double? PotassiumDl { get; set; }

        [Column("kjel_n_tot_water")]
        public double? KjeldahlNitrogen { get; set; }
        [Column("kjel_n_tot_water_dl")]
        public double? KjeldahlNitrogenDl { get; set; }

        [Column("mg_diss_water")]
        public double? Magnesium { get; set; }
        [Column("mg_diss_water_dl")]
        public double? MagnesiumDl { get; set; }

        [Column("na_diss_water")]
        public double? Sodium { get; set; }
        [Column("na_diss_water_dl")]
        public double? SodiumDl { get; set; }

        [Column("nh4_n_diss_water")]
        public double? Ammonia { get; set; }
        [Column("nh4_n_diss_water_dl")]
        public double? AmmoniaDl { get; set; }

        [Column("no3_no2_n_diss_water")]
        public double? Nitrates { get; set; }
        [Column("no3_no2_n_diss_water_dl")]
        public double? NitratesDl { get; set; }

        [Column("p_tot_water")]
        public double? TotalPhosphorus { get; set; }
        [Column("p_tot_water_dl")]
        public double? TotalPhosphorusDl { get; set; }

        [Column("ph_diss_water")]
        public double? PhLevel { get; set; }
        [Column("ph_diss_water_dl")]
        public double? PhLevelDl { get; set; }

        [Column("po4_p_diss_water")]
        public double? Phosphates { get; set; }
        [Column("po4_p_diss_water_dl")]
        public double? PhosphatesDl { get; set; }

        [Column("si_diss_water")]
        public double? Silica { get; set; }
        [Column("si_diss_water_dl")]
        public double? SilicaDl { get; set; }

        [Column("so4_diss_water")]
        public double? Sulfate { get; set; }
        [Column("so4_diss_water_dl")]
        public double? SulfateDl { get; set; }

        [Column("tal_diss_water")]
        public double? TotalAlkalinity { get; set; }
        [Column("tal_diss_water_dl")]
        public double? TotalAlkalinityDl { get; set; }

        // --- STATION & TAGS ---
        [Column("station")]
        public string? Station { get; set; }

        [Column("qat")]
        public string? QualityTag { get; set; }
    }
}