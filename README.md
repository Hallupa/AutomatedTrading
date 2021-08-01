Note this project is a work-in-progress - there's still lots to be done

# Trade Bot designer and runner

This is created in C# in Visual Studio 2019. It is an experimental work-in-progress to create an application to design crypto or spread betting trading bots.
It also will run then live (Currently only with FXCM).

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
This is still highly experimental but a lot of work has been done so far.

At present I'm just focusing on making this useful for me but if others are interested in this also, I will take feedback and maybe add an installer.
Any feedback or ideas are welcome!

## Strategy editor screen
![Screenshot](https://github.com/Hallupa/AutomatedTrading/blob/master/Docs/Images/EditStrategy.png)
On the right is the strategy editor section - this shows a strategy created for placing random trades.
Coding strategies has been designed to be as simple as possible as shown in the screenshot.
The log at the bottom of the screenshot demonstrates  just how fast a strategy can run - 33 markets run in 35 seconds creating 105k trades over the 10 years of data.

## Trade chart
![Screenshot](https://github.com/Hallupa/AutomatedTrading/blob/master/Docs/Images/TradeChart.png)
This shows all the trades found for the strategy - these can then be shown on the trade chart.

## View all trades
![Screenshot](https://github.com/Hallupa/AutomatedTrading/blob/master/Docs/Images/ViewAllTrades.png)
View all trades on a single chart.

## Equity chart
![Screenshot](https://github.com/Hallupa/AutomatedTrading/blob/master/Docs/Images/EquityResults.png)
This is the chart of equity over the time of the simulated trades. This is the random trades strategy which results in the account dropping to zero.

## Machine learning
![Screenshot](https://github.com/Hallupa/AutomatedTrading/blob/master/Docs/Images/MachineLearning.png)
Set points of interest on the chart which will then feed into the TensorFlow machine learning, which will use a neural network to learn the data.
