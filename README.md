Contrib.Nuget
=============

Project extending nuget with additional features:

## Baseclass.Contrib.Nuget.Output

Treats the "output" folder as an additional special folder and copies it's content to the build folder

http://www.nuget.org/packages/Baseclass.Contrib.Nuget.Output/

Add this package to your packages dependencies

	<dependencies>
            <dependency id="Baseclass.Contrib.Nuget.Output" />
	</dependencies>

and every file you add to your packages output folder will be copied to the projects output folder.

	<files>
        <file src="output\native.dll" target="output\native.dll" />
		<file src="output\autoresolved.dll" target="output\autoresolved.dll" />
		<file src="output\readme.txt" target="output\readme.txt" />
    </files>
	
