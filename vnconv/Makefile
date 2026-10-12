SRCS = charset.cpp convert.cpp data.cpp error.cpp pattern.cpp
OBJS = charset.o convert.o data.o error.o pattern.o
INCLUDE_DIR = byteio
OPTFLAGS="-O3"

all: libvnconv.a

libvnconv.a : ${OBJS} libbyteio
	cp byteio/libbyteio.a $@
	${AR} ru $@ ${OBJS}
	ranlib $@

${OBJS}: %.o: %.cpp
	${CXX} ${CXXFLAGS} ${OPTFLAGS} -c $< -o $@ -I${INCLUDE_DIR}

.PHONY: libbyteio
libbyteio:
	cd byteio && make all

clean:
	rm *.o
	cd byteio && make clean
#	rm libvnconv.a

depend:
	makedepend -- ${CFLAGS} -- ${SRCS}
	cd byteio && make depend
# DO NOT DELETE

