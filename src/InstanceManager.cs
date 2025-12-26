namespace ClunkyBorders;

internal class InstanceManager : IDisposable
{
    private const string MutexName = "Global\\ClunkyBorders_7E826ED2-5326-4891-B91D-3A7B38522AD4";
    private Mutex? mutex;
    private bool instanceExists;

    public bool IsSingleInstance()
    {
        try
        {
            mutex = new Mutex(
                initiallyOwned: true,
                name: MutexName,
                createdNew: out bool createdNew);

            if(createdNew)
            {
                instanceExists = true;
                return true;
            }
            else
            {
                instanceExists = mutex.WaitOne(0);
                return instanceExists;
            }
        }
        catch(AbandonedMutexException)
        {
            instanceExists = true;
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"InstanceManager. Error checking instance.", ex);
            return false;
        }
    }

    public void Dispose()
    {
        if(mutex != null && instanceExists)
        {
            try
            {
                mutex.ReleaseMutex();
                instanceExists = false;
            }
            catch (Exception ex)
            {
                Logger.Error($"InstanceManager. Error releasing instance lock.", ex);
            }
        }

        mutex?.Dispose();
        mutex = null;
    }
}
