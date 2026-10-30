# probe — run a calculator from the command line

Verification aid for Addendum C Track P. It is not in the solution and not in CI.

```
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet
dotnet build tools/verification/probe -c Release
P=tools/verification/probe/bin/Release/net10.0/Probe
$P list
$P kat mech.bearing-life
$P run mech.bearing-life inputs.json     # inputs: [{"Symbol":"F_r","Number":2,"Unit":"kN"}, {"Symbol":"type","Text":"ball (p = 3)"}]
$P steps mech.bearing-life inputs.json   # also prints every worksheet step
$P template mech.bearing-life
```
Exit codes: 0 ok, 1 a known-answer case failed, 2 usage, 3 the run was blocked.
