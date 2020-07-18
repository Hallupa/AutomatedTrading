# Trade Bot designer and runner

This is created in C# and is an experimental work-in-progress to create an application to design spread betting trading bots and run live against FXCM.
I've been creating this to design and run my own trading strategies.

## Project aims
1. Create high performance desktop applicate
2. Make designing strategies as easy as possible
3. Ability to design trading bot strategies that run against 1 hour candles and up
4. Run strategies against fast - i.e. 30 markets with 10 years of data in 30 seconds
5. Ability to view trades on a candle chart
6. Show statistics such as expectancy, drawdown, etc
7. Ability to download candle data from FXCM
8. Ability to run the strategy live against FXCM
9. Add machine learning support

## Progress so far
From the project aims, I have everything above 1-9 done in a basic form. Below are screenshots of progress so far. This is still highly experimental.

Any feedback or ideas are welcome!

## Strategy editor screen
![Screenshot](https://github.com/Hallupa/AutomatedTrading/blob/master/Docs/Images/EditStrategy.png)
On the right is the strategy editor section - this shows a strategy created for placing random trades.
Coding strategies has been designed to be as simple as possible as shown in the screenshot.
The log at the bottom of the screenshot demonstrates  just how fast a strategy can run - 33 markets run in 35 seconds creating 105k trades over the 10 years of data.

## Trade chart
![Screenshot](https://github.com/Hallupa/AutomatedTrading/blob/master/Docs/Images/TradeChart.png)
This shows all the trades found for the strategy - these can then be shown on the trade chart.

## Equity chart
![Screenshot](https://github.com/Hallupa/AutomatedTrading/blob/master/Docs/Images/EquityResults.png)
This is the chart of equity over the time of the simulated trades. This is the random trades strategy which results in the account dropping to zero.
