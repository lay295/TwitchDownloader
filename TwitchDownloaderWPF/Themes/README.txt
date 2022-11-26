This app supports user created themes!

To get started, simply duplicate one of the included themes and open it with your favorite
text editor.


HandyControl elements do not provide full theming support, however they do provide a dark
variant, hence the dedicated boolean key.


The Boolean keys control several things, such as the title bar theme and
the HandyControl theme.


The SolidColorBrush keys control the color of the application and app elements, such as the
app background, text, and border colors.

The 'Inner' keys are used to add depth to double-recursive elements.
The following diagram illustrates this:

/----------------------------[-][#][x]
|           AppBackground           |
| /-------------------------------\ |
| |     AppElementBackground      | |
| | /---------------------------\ | |
| | | AppInnerElementBackground | | |
| | |                           | | |
| | \---------------------------/ | |
| |                               | |
| \-------------------------------/ |
|                                   |
\-----------------------------------/
In this case AppElementBackground is being used by a frame, while AppInnerElementBackground
is being used by a bordered label, blank image background, or similar.


If you created a theme that you believe should be included with the program, feel free
to make a pull request on the github! https://github.com/lay295/TwitchDownloader/pulls


Note: File names are read in a non-case sensitive manner, meaning
'Dark.xaml' and 'dark.xaml' cannot be differentiated.