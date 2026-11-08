# Racing Cars Controller

Android and Windows apps to control Bluetooth cars from the Shell Motorsport collection, BMW M Motorsport collection and Shell Racing Legends - Ferrari collections.

## Download

### Android
Run the **Build Android APK** workflow from the Actions tab, then download the
`ShellCar-Controller-APK` artifact. The APK is signed with a temporary build key;
installing an APK from a later workflow run may require uninstalling the previous
version first.
For local signed, per-architecture APKs on Windows, run `CreateApk.ps1` with
`-newVersionName` and `-signingKeyPass`; packages are copied to `artifacts/` by
default.

### Windows
<a href='https://apps.microsoft.com/detail/9N8MXSKCDF9J?cid=github&mode=full'>
	<img src='https://get.microsoft.com/images/en-us%20dark.svg' width='200' alt='Download from Microsoft Store'/>
</a>

## Android Bluetooth controller

Pair an Xbox-compatible Bluetooth controller with the Android device, open the
app, scan for the car, and connect. The controller mappings are:

| Controller input | Car action |
| --- | --- |
| A, right bumper, or D-pad up | Forward |
| X, left bumper, or D-pad down | Reverse |
| Left stick horizontal axis or D-pad left/right | Steering |
| Right trigger | Forward throttle |
| Left trigger | Reverse throttle |
| B | Toggle lights |
| Y | Toggle turbo |

The on-screen controls remain available. On Android, allow Bluetooth access
when prompted and connect the car before driving.



## Screenshots

![alt text](https://github.com/tmk907/RacingCarsController/blob/master/files/images/RacingCarsControllerAndroid.jpg "Android app screenshot")  
![alt text](https://github.com/tmk907/RacingCarsController/blob/master/files/images/RacingCarsControllerDesktop.png "Windows app screenshot")


## Links
- https://gist.github.com/scrool/e79d6a4cb50c26499746f4fe473b3768
- https://github.com/martonmiklos/sailfish-ble-rc
- https://github.com/Freeyourgadget/Gadgetbridge
