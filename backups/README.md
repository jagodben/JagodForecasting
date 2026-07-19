# Forecast history backups

`forecast-export.json.gz` is a nightly full dump of the API's persisted model state —
forecast history, chamber control lines, the generic-ballot series, polls, nominee
overrides, and settings — fetched from `GET /api/forecast/export` by the
[backup workflow](../.github/workflows/backup.yml) at 9:45 AM ET (after the daily
8 AM snapshot). Every value in it is public data the site already serves; this file
just makes the track record durable if the server's disk is ever lost. Older backups
live in this file's git history.

## Restoring after a disk loss

1. Grab the newest good backup (this folder, or an earlier commit of it) and unzip:
   `gunzip -k forecast-export.json.gz`
2. In Render, set an `ADMIN_KEY` environment variable on the API service (any long
   random string) and let it redeploy.
3. Send the backup to the import endpoint:

   ```
   curl -X POST https://api.jagodforecasting.com/api/forecast/import \
     -H "X-Admin-Key: <the key>" \
     -H "Content-Type: application/json" \
     --data-binary @forecast-export.json
   ```

4. The response reports how many rows were inserted per table. The import obeys the
   immutability rule — it only inserts rows whose (race, date) doesn't exist, so
   anything recorded since the backup stays untouched, and re-running it is harmless.
5. Remove `ADMIN_KEY` again if you want the admin endpoints to go back to 404.
