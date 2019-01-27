FROM andmos/dotnet-script

COPY lib/ lib/
COPY main.csx main.csx

CMD ["main.csx"]