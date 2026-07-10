#!/usr/bin/env bash
# Builds election-forecaster-client/public/data/districts.json — the House map geometry
# on the lines actually in use for the 2026 elections.
#
# Recipe: Census cb_2024_us_cd119_20m (2024 lines) as the national base, with the ten
# mid-decade-redrawn states (AL, CA, FL, LA, MO, NC, OH, TN, TX, UT) replaced by their
# officially published 2026 plans:
#   TX  PLANC2333 shapefile        — data.capitol.texas.gov (CKAN dataset "planc2333")
#   NC  SL 2025-95 shapefile       — dl.ncsbe.gov S3 bucket, ShapeFiles/USCongress/
#   MO  HB1 2025 shapefile         — MSDIS ArcGIS item ee1971b86cce43d4b92b5ce614866a18
#   AL  2023 Congressional Plan    — sos.alabama.gov (certified for 2026 on 2026-06-10)
#   CA  Prop 50 / AB 604           — CA Geoportal FeatureServer (services3…AB_604…view)
#   OH  Districts_2025_11_03       — maps.ohio.gov Hosted FeatureServer (OGRIP item)
#   UT  2026-2032 districts        — Utah AGRC FeatureServer
#   FL  May 2026 districts         — FL Geographic Information Office FeatureServer
#   TN  Congressional Districts    — tnmap.tn.gov LEGISLATIVE_DISTRICTS MapServer/2
#   LA  2026 Plan                  — South Central Planning FeatureServer
#
# Inputs (downloaded beforehand into $WORK): cd119/*.shp, tx/, nc/, mo/, al/ shapefile
# dirs and ca/oh/ut/fl/tn/la .geojson FeatureServer exports (f=geojson&outSR=4326).
# Output schema per feature: STATEFP, DISTRICT ('00' = at-large), GEOID, NAMELSAD.
set -euo pipefail
WORK="${1:?usage: build_districts_topojson.sh <workdir>}"
OUT="$(dirname "$0")/../election-forecaster-client/public/data/districts.json"
cd "$WORK"
MS="npx -y mapshaper"

norm () { # norm <input> <fips> <district-expr> <output>
  $MS "$1" -proj wgs84 \
    -each "STATEFP='$2', DISTRICT=String($3).padStart(2,'0')" \
    -filter-fields STATEFP,DISTRICT \
    -simplify interval=1500 keep-shapes \
    -o "$4"
}

norm "tx/PLANC2333/PLANC2333.shp"        48 "District"    n_tx.geojson
norm "nc/SL 2025-95.shp"                 37 "DISTRICT"    n_nc.geojson
norm "mo/HB1_Cong_Dist_2025.shp"         29 "District"    n_mo.geojson
norm "al/2023_CONG_LEGISLATIVE_PLAN.shp" 01 "DISTRICT"    n_al.geojson
norm "ca.geojson"                        06 "DISTRICT"    n_ca.geojson
norm "oh.geojson"                        39 "district"    n_oh.geojson
norm "ut.geojson"                        49 "DISTRICT"    n_ut.geojson
norm "fl.geojson"                        12 "DISTRICT"    n_fl.geojson
norm "tn.geojson"                        47 "DISTRICT"    n_tn.geojson
norm "la.geojson"                        22 "DISTRICT_I"  n_la.geojson

# National base minus the ten replaced states; keep the same schema.
$MS cd119/cb_2024_us_cd119_20m.shp \
  -filter "!['01','06','12','22','29','37','39','47','48','49'].includes(STATEFP)" \
  -each "DISTRICT=CD119FP" \
  -filter-fields STATEFP,DISTRICT \
  -o n_base.geojson

$MS -i n_base.geojson n_tx.geojson n_nc.geojson n_mo.geojson n_al.geojson \
       n_ca.geojson n_oh.geojson n_ut.geojson n_fl.geojson n_tn.geojson n_la.geojson \
       combine-files \
  -merge-layers force \
  -each "GEOID=STATEFP+DISTRICT, NAMELSAD='Congressional District '+(DISTRICT=='00' ? '(at Large)' : +DISTRICT)" \
  -rename-layers districts_2026 \
  -o format=topojson quantization=100000 "$OUT"

echo "wrote $OUT"
