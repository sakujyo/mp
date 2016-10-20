#include <stdio.h>
#include <stdlib.h>

int main(int argc, char *argv[]) {
	FILE *fp, *outp;
	char bufw[4];
	char bufh[4];
	char bufb[4];
	//char bufp[4 * 1920 * 4];

	//printf("argv[1]: %s\n", argv[1]);
	int d = atoi(argv[2]);
	int tx = atoi(argv[3]);
	int ty = atoi(argv[4]);
	int tw = atoi(argv[5]);
	int th = atoi(argv[6]);
	printf("thinning: %d\n", d);
	if ((fp = fopen(argv[1], "r")) == NULL) {
		printf("%s open error.\n", argv[1]);
		exit(EXIT_FAILURE);
	}
	fread(bufw, (size_t)1, (size_t)4, fp);
	//printf("%d, %d, %d, %d\n", bufw[0], bufw[1], bufw[2], bufw[3]);
	int width = *((int *)bufw);
	fread(bufh, (size_t)1, (size_t)4, fp);
	int height = *((int *)bufh);
	fread(bufb, (size_t)1, (size_t)4, fp);
	int bpp = *((int *)bufb);
	printf("w, h, b: %d, %d, %d\n", width, height, bpp);

	if ((outp = fopen(argv[7], "wb")) == NULL) {
		printf("%s open error.\n", argv[3]);
		exit(EXIT_FAILURE);
	}

	char bufp[4 * width * d];
	//fseek(fp, (long)(12 + width * 4 * y + x * 4), SEEK_SET);
	//fread(bufp, (size_t)1, (size_t)4 * width * height, fp);
	//for (int y = 0; y < height / d; y++) {
	for (int y = ty; y < ty + th; y++) {
		fseek(fp, (long)(12 + width * y * d * 4), SEEK_SET);
		fread(bufp, (size_t)1, (size_t)4 * width * d, fp);
		for (int x = tx; x < tx + tw; x++) {
			int r = 0;
			int g = 0;
			int b = 0;
			for (int w = 0; w < d; w++) {
				for (int v = 0; v < d; v++) {
					int yy = w;
					int xx = x * d + v;
					//printf("x = %d, y = %d. ", xx, yy);
					r += (unsigned int)bufp[4 * (width * yy + xx) + 0];
					g += (unsigned int)bufp[4 * (width * yy + xx) + 1];
					b += (unsigned int)bufp[4 * (width * yy + xx) + 2];
				}
			}
			char cr = (char)(r / (d * d));
			char cg = (char)(g / (d * d));
			char cb = (char)(b / (d * d));
			fwrite(&cr, (size_t)1, (size_t)1, outp);
			fwrite(&cg, (size_t)1, (size_t)1, outp);
			fwrite(&cb, (size_t)1, (size_t)1, outp);
		}
		//printf("\n");
	}

	fclose(outp);
	fclose(fp);
}
