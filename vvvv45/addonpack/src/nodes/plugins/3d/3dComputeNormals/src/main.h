#pragma once

#include <stdexcept>
using namespace std;

namespace ComputeN
{
	extern "C" { __declspec(dllexport) void ComputeNormals(double *Vertices, int *TriangleI, double *faceN, double *vertexN, double *cornerN, double dihedral, unsigned int enumMode, int *binSizes, bool computeFace, bool computeVertex, bool computeCorner); }//, int *PEext);}
	extern "C" __declspec(dllexport) int ReleaseMemory(double* pArray)
	{
		delete[] pArray;
		//delete[] Usize;
		return 0;
	}
}

