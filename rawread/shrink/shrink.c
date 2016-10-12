#include <stdio.h>
#include <stdlib.h>

int main(int argc, char *argv[]) {
	FILE *fp, *outp;
	char bufw[4];
	char bufh[4];
	char bufb[4];
	char bufp[4];

	//printf("argv[1]: %s\n", argv[1]);
	int d = atoi(argv[2]);
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
	printf("w, h, b: %d, %d, %d", width, height, bpp);

	if ((outp = fopen(argv[3], "wb")) == NULL) {
		printf("%s open error.\n", argv[3]);
		exit(EXIT_FAILURE);
	}

	for (int y = 0; y < height; y++) {
		for (int x = 0; x < width; x++) {
			fread(bufp, (size_t)1, (size_t)4, fp);
			if ((y % d) == 0) {
				if ((x % d) == 0) {
					fwrite(bufp, (size_t)1, (size_t)3, outp);
				}
			}
		}
	}

	fclose(outp);
	fclose(fp);
}
