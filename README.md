![Icon](Source/Assets/AppIcon.png)
# WinUI - NuGet Package Cleaner


## 📝 v1.0.0.0 - January 2025

**Dependencies**

| Assembly | Version |
| ---- | ---- |
| .NET Core | 8.0 |
| Microsoft.WindowsAppSDK | 1.5.2 |
| Microsoft.Windows.SDK.BuildTools | 10.0.22621 |

## 🎛️ Description
- A cleaner utility for outdated packages which can consume a large amount of space on your local storage.
	- The `Report Only` mode will scan for and display package total sizes based on the stale amount (in days).
	- Because this is meant to run as a self-contained/stand-alone utility, I suggest leaving the profile as `Unpackaged`.
	- I'll admit I went a bit overboard: making custom controls, custom dialogs, embedded asset extentions, toast notification helpers, encrypted profile setting reader/writer, window message interception, et. al. but don't forget to give a ⭐ if you find any of my code helpful or educational.

## 💻 Screenshot
![Sample](Source/Assets/Screenshot.png)

![ContentDialog](Source/Assets/Screenshot2.png)

![ContentDialog](Source/Assets/Screenshot3.png)

![ContextMenu](Source/Assets/Screenshot4.png)

![Toast](Source/Assets/Screenshot5.png)

![TaskBar](Source/Assets/Screenshot6.png)

## 🧾 License/Warranty
* Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish and distribute copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
* The software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In no event shall the author or copyright holder be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the software or the use or other dealings in the software.
* Copyright © 2023-2025. All rights reserved.

## 📋 Proofing
* This application was compiled and tested using *VisualStudio* 2022 on *Windows 10* versions **22H2**, **21H2**, **21H1** and *Windows 11* versions **24H2**, **23H2**.
