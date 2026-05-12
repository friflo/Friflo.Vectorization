

## CallbackMode


### WaitAnyOnly
*Behavior*  
Callback is queued and "sleeps" until explicitly signaled.  
*Required Action*  
call `wgpuInstanceWaitAny()` - blocking call
*Use-case*  
Used if you can not proceed before GPU finished

### AllowProcessEvents
*Behavior*  
Callback is processed during explicit event polling  
*Required Action*  
call `wgpuInstanceProcessEvents()` - non blocking call in a loop  
*Use-case*  


### AllowSpontaneous
*Behavior*  
Callback can be triggered at any time (often from a background thread)  
*Required Action*  
None (but requires strict Thread-Safety!)

