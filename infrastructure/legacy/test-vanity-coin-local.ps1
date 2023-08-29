docker run -it --rm `
    --mount type=bind,source=c:\src\vanitycoindeployment,target=/app `
    registry.digitalocean.com/rivalcoins/kelp:v1.11.0 `
    /bin/kelp trade --botConf /app/jeromebellsr-trader.cfg `
    --strategy balanced `
    --stratConf /app/balanced-strategy.cfg `
    --no-headers