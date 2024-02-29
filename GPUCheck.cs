using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Management;

namespace VideoCompressor
{
	public static class GPUCheck
	{
		public enum GPUVendor
		{
			NVIDIA,
			NVIDIA_RTX,
			AMD,
			INTEL,
			UNKNOWN
		}
		public static GPUVendor GetVendor()
		{
			GPUVendor vendor = GPUVendor.UNKNOWN;

			ManagementObjectSearcher searcher = new
			ManagementObjectSearcher("SELECT * FROM Win32_VideoController");

			string gpuDesc;
			foreach (ManagementObject obj in searcher.Get())
			{
				foreach (PropertyData prop in obj.Properties)
				{

					if (prop.Name == "Description")
					{
						gpuDesc = prop.Value.ToString().ToUpper();

						if (gpuDesc.Contains("INTEL") == true)
						{
							vendor = GPUVendor.INTEL;
						}
						else if (gpuDesc.Contains("AMD") == true)
						{
							vendor = GPUVendor.AMD;
						}
						else if (gpuDesc.Contains("NVIDIA") == true)
						{
							vendor = GPUVendor.NVIDIA;
							if (gpuDesc.Contains("RTX") == true)
							{
								vendor = GPUVendor.NVIDIA_RTX;
							}
						}
						else
						{
							vendor = GPUVendor.UNKNOWN;
						}
					}
				}
			}
			return vendor;
		}
	}
}
