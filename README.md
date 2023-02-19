## Repository description
<p>This is the main REVIGO Core library that implements the whole clustering algorithm.</p>
<p>Since a lot of changes had to be made for .NET Core. Official release with binaries is planned for mid February 2023</p>

## How to compile and use this library
<p>To compile from command line: 
<ul>
	<li>Optional: Install <a href="https://visualstudio.microsoft.com/">Visual Studio Code</a> or <a href="https://visualstudio.microsoft.com/">Visual Studio for Windows</a> (You can also compile from Visual Studio for Windows)</li>
	<li>Install .NET core 6.0 from Microsoft (<a href="https://dotnet.microsoft.com/download">Install .NET for Windows</a>, <a href="https://learn.microsoft.com/en-us/dotnet/core/install/linux">Install .NET for Linux</a>)</li>
	<li>git clone https://github.com/rajko-horvat/RevigoCore</li>
	<li>dotnet build --configuration Release --os win-x64 RevigoCore.csproj (For Linux use --os linux. See <a href="https://learn.microsoft.com/en-us/dotnet/core/rid-catalog">list of OS RIDs</a> for --os option)</li>
	<li>Copy generated binary files (under RevigoCore/bin/net6.0/) to your project and enjoy.</li>
</ul></p>

## About REVIGO (REduce + VIsualize Gene Ontology) project
<p>Outcomes of high-throughput biological experiments are typically interpreted by statistical testing
for enriched gene functional categories defined by the Gene Ontology (GO). The resulting lists of GO terms 
may be large and highly redundant, and thus difficult to interpret.<p>
<p>REVIGO is a successful project to summarize long, unintelligible lists of Gene Ontology terms by finding a representative subset 
of the terms using a simple clustering algorithm that relies on semantic similarity measures.</p>
<p>For any further information about REVIGO project please see 
<a href="https://dx.doi.org/10.1371/journal.pone.0021800" target="_blank">published paper</a> and 
<a href="http://revigo.irb.hr/FAQ.aspx" target="_blank">Frequently Asked Questions page</a></p>

## Algorithm to reduce redundancy within lists of GO terms
<p>Researchers analyzing annotations of gene products are often faced with long lists of GO terms 
that are either close in the GO hierarchy (sibling terms) or are related by inheritance (child and parent terms). 
These redundant lists are difficult to interpret, but are likely to contain clusters of semantically similar GO terms.</p>
<p>To mitigate the problem of large and redundant lists, we aim to find a single representative GO term for each of these clusters. 
REVIGO performs a simple clustering procedure which is in concept similar to the hierarchical (agglomerative) clustering methods 
such as the neighbor joining approach.</p>

<p align="center"><img src="Flowchart.png" alt="Flowchart of the algorithm" style="width:577px; height:789px;" /><br/>
<i>A flowchart of the simplified algorithm to reduce redundancy.</i></p>
