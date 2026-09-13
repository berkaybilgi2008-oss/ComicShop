#include <cmath>
#include <algorithm>
extern "C" void raster(const double* v,const double* n,const int* f,int count,const double* col,int w,int h,double* depth,unsigned char* rgb) {
 const double light[3]={-0.4138029443,0.6896715738,0.5977153640};
 for(int face=0;face<count;face++) {
  int ia=f[face*3],ib=f[face*3+1],ic=f[face*3+2];
  const double *a=v+3*ia,*b=v+3*ib,*c=v+3*ic;
  double den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1]);
  if(std::abs(den)<1e-8)continue;
  int x0=std::max(0,(int)std::floor(std::min({a[0],b[0],c[0]})));
  int x1=std::min(w-1,(int)std::ceil(std::max({a[0],b[0],c[0]})));
  int y0=std::max(0,(int)std::floor(std::min({a[1],b[1],c[1]})));
  int y1=std::min(h-1,(int)std::ceil(std::max({a[1],b[1],c[1]})));
  for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++) {
   double u=((b[1]-c[1])*(x+.5-c[0])+(c[0]-b[0])*(y+.5-c[1]))/den;
   double t=((c[1]-a[1])*(x+.5-c[0])+(a[0]-c[0])*(y+.5-c[1]))/den;
   double s=1-u-t;if(u<0||t<0||s<0)continue;
   double z=u*a[2]+t*b[2]+s*c[2];int index=y*w+x;if(z<=depth[index])continue;
   double normal[3],len=0,diffuse=0;
   for(int k=0;k<3;k++){normal[k]=u*n[3*ia+k]+t*n[3*ib+k]+s*n[3*ic+k];len+=normal[k]*normal[k];}
   len=std::sqrt(len);for(int k=0;k<3;k++)diffuse+=normal[k]*light[k]/std::max(len,1e-10);
   double shade=diffuse>.6?1.05:(diffuse>.05?.81:.46);depth[index]=z;
   for(int k=0;k<3;k++)rgb[3*index+k]=(unsigned char)std::clamp(col[k]*shade,0.,255.);
  }
 }
}
